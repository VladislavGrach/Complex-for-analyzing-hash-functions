document.addEventListener("DOMContentLoaded", function () {
    const data = window.hashAnalysisData;
    if (!data || !data.rounds || !data.metrics) return;

    const rounds = data.rounds;
    const metricsArr = data.metrics;
    const chartInstances = {};

    // 1. Функция извлечения серии (mean + границы) с заменой NaN на 0
    function series(name) {
        // Получаем mean с заменой null/NaN на 0
        const mean = metricsArr.map(m => {
            if (!m || !m[name]) return 0;
            const val = m[name].Mean;
            return (val !== null && !isNaN(val)) ? val : 0;
        });

        // Получаем upper/lower с сервера или из Ci
        const upperFromServer = metricsArr.map(m => {
            if (!m || !m[name]) return 0;
            const val = m[name].Upper ?? null;
            return (val !== null && !isNaN(val)) ? val : 0;
        });

        const lowerFromServer = metricsArr.map(m => {
            if (!m || !m[name]) return 0;
            const val = m[name].Lower ?? null;
            return (val !== null && !isNaN(val)) ? val : 0;
        });

        const hasBounds = upperFromServer.some(v => v !== 0) && lowerFromServer.some(v => v !== 0);

        if (hasBounds) {
            return { mean, upper: upperFromServer, lower: lowerFromServer };
        }

        // Fallback: mean ± Ci
        const ci = metricsArr.map(m => {
            if (!m || !m[name]) return 0;
            const val = m[name].Ci ?? 0;
            return !isNaN(val) ? val : 0;
        });

        const upper = mean.map((v, i) => v + ci[i]);
        const lower = mean.map((v, i) => v - ci[i]);

        return { mean, upper, lower };
    }

    // 2. Плагины Chart.js
    const whiteBackgroundPlugin = {
        id: 'whiteBackground',
        beforeDraw: (chart) => {
            const ctx = chart.ctx;
            ctx.save();
            ctx.globalCompositeOperation = 'destination-over';
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, chart.width, chart.height);
            ctx.restore();
        }
    };

    // 3. Универсальная функция отрисовки графика
    function drawChart(id, title, yLabel, mean, upper, lower, color, yMin = null, yMax = null, testKey = null) {
        const canvas = document.getElementById(id);
        if (!canvas) return;

        if (Chart.getChart(canvas)) {
            Chart.getChart(canvas).destroy();
        }

        const cardBody = canvas.closest(".card-body");
        const oldButtons = cardBody?.querySelector(".chart-export-buttons");
        if (oldButtons) {
            oldButtons.remove();
        }

        const chart = new Chart(canvas, {
            type: 'line',
            plugins: [whiteBackgroundPlugin],
            data: {
                labels: rounds,
                datasets: [
                    {
                        data: upper,
                        borderWidth: 0,
                        pointRadius: 0,
                        showLine: false,
                        pointHoverRadius: 0,
                        pointHitRadius: 0,
                        tension: 0.35,
                        cubicInterpolationMode: 'monotone',
                        spanGaps: true
                    },
                    {
                        label: 'Доверительный интервал (±σ)',
                        data: lower,
                        fill: '-1',
                        backgroundColor: color.zone,
                        borderWidth: 0,
                        pointRadius: 0,
                        showLine: false,
                        pointHoverRadius: 0,
                        pointHitRadius: 0,
                        tension: 0.35,
                        cubicInterpolationMode: 'monotone',
                        spanGaps: true
                    },
                    {
                        label: 'Среднее значение',
                        data: mean,
                        borderColor: color.line,
                        backgroundColor: color.line,
                        borderWidth: 2.5,
                        pointRadius: 3,
                        pointHitRadius: 10,
                        tension: 0.35,
                        cubicInterpolationMode: 'monotone',
                        fill: false,
                        spanGaps: true
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            filter: (item) => item.text !== undefined,
                            usePointStyle: true,
                            pointStyleWidth: 12,
                            generateLabels: function (chart) {
                                const labels = Chart.defaults.plugins.legend.labels.generateLabels(chart);
                                for (const l of labels) {
                                    if (l.datasetIndex === 2) {
                                        const ds = chart.data.datasets[2];
                                        const c = ds.backgroundColor || ds.borderColor;
                                        l.usePointStyle = true;
                                        l.pointStyle = 'circle';
                                        l.fillStyle = c;
                                        l.strokeStyle = c;
                                        l.lineWidth = 0;
                                    } else {
                                        l.usePointStyle = false;
                                    }
                                }
                                return labels;
                            }
                        }
                    },
                    title: { display: true, text: title },
                    tooltip: {
                        enabled: true,
                        position: 'nearest',
                        displayColors: true,
                        callbacks: {
                            title: function (items) {
                                const chart = items[0]?.chart;
                                if (!chart) return null;
                                return `Количество раундов: ${items[0].label}`;
                            },
                            beforeBody: function (items) {
                                const i = items[0].dataIndex;
                                const chart = items[0]?.chart;
                                if (!chart) return [];

                                const upperVal = chart.data.datasets[0].data[i];
                                const lowerVal = chart.data.datasets[1].data[i];
                                const meanVal = chart.data.datasets[2].data[i];

                                const fmt = (v) => Number(v).toFixed(6).replace('.', ',');
                                const lines = [];

                                lines.push(`Среднее значение: ${fmt(meanVal)}`);
                                lines.push(`Верхняя граница: ${fmt(upperVal)}`);
                                lines.push(`Нижняя граница: ${fmt(lowerVal)}`);

                                // Добавляем предупреждение, если значение было NaN и заменено на 0
                                const originalMean = metricsArr[i]?.[testKey]?.Mean;
                                if (originalMean === null || isNaN(originalMean)) {
                                    lines.push(`⚠️ Значение отсутствовало! Заменено на 0`);
                                }

                                return lines;
                            },
                            label: function () { return ''; }
                        }
                    }
                },
                scales: {
                    x: { title: { display: true, text: 'Число раундов' } },
                    y: {
                        min: yMin,
                        max: yMax,
                        title: { display: true, text: yLabel },
                        ticks: { stepSize: 0.2 },
                        beginAtZero: true
                    }
                }
            }
        });

        chartInstances[id] = chart;

        // Кнопки экспорта
        const btnGroup = document.createElement("div");
        btnGroup.className = "d-flex gap-2 mt-3 flex-wrap chart-export-buttons";

        // Функция для получения безопасного имени файла (удаляем только недопустимые символы)
        function getSafeFileName(testName, algorithm) {
            let safeName = testName.replace(/[\\/:*?"<>|]/g, '_');
            safeName = safeName.replace(/\s+/g, ' ');
            return `${safeName} ${algorithm}`;
        }

        const csvBtn = document.createElement("button");
        csvBtn.className = "btn btn-outline-secondary btn-sm";
        csvBtn.textContent = "Экспорт CSV";
        csvBtn.onclick = () => {
            let csv = "Раунд;Среднее;Верхняя граница;Нижняя граница;Исходное значение отсутствовало\n";
            rounds.forEach((r, i) => {
                const wasNaN = metricsArr[i]?.[testKey]?.Mean === null || isNaN(metricsArr[i]?.[testKey]?.Mean);
                csv += `${r};${mean[i]};${upper[i]};${lower[i]};${wasNaN ? 'Да' : 'Нет'}\n`;
            });
            // Заменяем точку на запятую для корректного отображения в Excel
            csv = csv.replace(/\./g, ',');
            const blob = new Blob(["\uFEFF" + csv], { type: "text/csv;charset=utf-8;" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            const fileName = getSafeFileName(title || testKey, window.currentAlgorithm);
            a.download = `${fileName}.csv`;
            a.click();
            URL.revokeObjectURL(url);
        };

        const jsonBtn = document.createElement("button");
        jsonBtn.className = "btn btn-outline-secondary btn-sm";
        jsonBtn.textContent = "Экспорт JSON";
        jsonBtn.onclick = () => {
            // В JSON сохраняем исходные значения с информацией о NaN
            const originalMean = metricsArr.map(m => m?.[testKey]?.Mean ?? null);
            const exportData = {
                "Алгоритм": window.currentAlgorithm,
                "Набор тестов": document.querySelector('input[name="suiteRadio"]:checked')?.value || 'diff',
                "Тест": title || testKey,
                "Раунды": rounds,
                "Отображаемое среднее": mean,
                "Исходное среднее": originalMean,
                "Верхняя граница": upper,
                "Нижняя граница": lower,
                "Примечание": "Значения 0 означают, что исходное значение отсутствовало (тест не удался)"
            };
            const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: "application/json" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            const fileName = getSafeFileName(title || testKey, window.currentAlgorithm);
            a.download = `${fileName}.json`;
            a.click();
            URL.revokeObjectURL(url);
        };

        const pngBtn = document.createElement("button");
        pngBtn.className = "btn btn-outline-secondary btn-sm";
        pngBtn.textContent = "Экспорт PNG";
        pngBtn.onclick = () => {
            const link = document.createElement("a");
            link.href = chart.toBase64Image();
            const fileName = getSafeFileName(title || testKey, window.currentAlgorithm);
            link.download = `${fileName}.png`;
            link.click();
        };

        btnGroup.appendChild(csvBtn);
        btnGroup.appendChild(jsonBtn);
        btnGroup.appendChild(pngBtn);

        if (cardBody) {
            cardBody.appendChild(btnGroup);
        }
    }

    // 4. Константы тестов
    const COLORS = {
        SAC: { line: '#1f77b4', zone: 'rgba(31,119,180,0.25)' },
        BIC: { line: '#d62728', zone: 'rgba(214,39,40,0.25)' },
        default: { line: '#2ca02c', zone: 'rgba(44,160,44,0.25)' }
    };

    const DIFF_TESTS = [
        { key: "SAC", title: "Строгий лавинный критерий (SAC)", yLabel: "Доля изменённых выходных битов", yMin: 0, yMax: 1, color: COLORS.SAC },
        { key: "BIC", title: "Критерий независимости битов (BIC)", yLabel: "Максимальная корреляция", yMin: -1.4, yMax: 2, color: COLORS.BIC }
    ];

    const NIST_TESTS = [
        { key: "Monobit", title: "Частотный побитовый тест", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "FrequencyWithinBlock", title: "Частотный блочный тест", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Runs", title: "Тест на последовательность одинаковых бит", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "LongestRunOfOnes", title: "Тест на самую длинную последовательность единиц в блоке", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "BinaryMatrixRank", title: "Тест рангов бинарных матриц", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "DiscreteFourier", title: "Спектральный тест", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "NonOverlappingTemplate", title: "Тест на совпадение непересекающихся шаблонов", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "OverlappingTemplate", title: "Тест на совпадение пересекающихся шаблонов", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "MaurerUniversal", title: "Универсальный статистический тест Маурера", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "LempelZiv", title: "Тест сжатия Лемпеля-Зива", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "LinearComplexity", title: "Тест на линейную сложность", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Serial", title: "Тест на периодичность", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "ApproximateEntropy", title: "Тест приближённой энтропии", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Cusum", title: "Тест кумулятивных сумм", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "RandomExcursions", title: "Тест случайных блужданий", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "RandomExcursionsVariant", title: "Модифицированный тест случайных блужданий", yLabel: "p-value", yMin: 0, yMax: 1 }
    ];

    const DIEHARD_TESTS = [
        { key: "BirthdaySpacings", title: "Тест распределения дней рождения", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "CountOnes", title: "Тест подсчёта единиц", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "MatrixRanks", title: "Тест рангов матриц ", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "OverlappingPermutations", title: "Тест на пересекающиеся перестановки", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "RunsDiehard", title: "Тест на серии", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Gcd", title: "Тест наибольшего общего делителя", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Squeeze", title: "Тест сжатия", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Craps", title: "Тест игры в крэпс", yLabel: "p-value", yMin: 0, yMax: 1 }
    ];

    const TESTU01_TESTS = [
        { key: "Collision", title: "Тест на коллизии", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Gap", title: "Тест на промежутки", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Autocorrelation", title: "Тест автокорреляции ", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "Spectral", title: "Спектральный тест", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "HammingWeight", title: "Тест веса Хэмминга", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "SerialTest", title: "Серийный тест", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "MultinomialTest", title: "Мультиномиальный тест", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "ClosePairs", title: "Тест близких пар", yLabel: "p-value", yMin: 0, yMax: 1 },
        { key: "CouponCollector", title: "Тест коллекционера купонов", yLabel: "p-value", yMin: 0, yMax: 1 }
    ];

    const ADDITIONAL_TESTS = [
        { key: "ChiSquare", title: "Критерий χ²", yLabel: "Значение χ²", yMin: 0, yMax: null },
        { key: "ShannonEntropy", title: "Энтропия Шеннона", yLabel: "Энтропия", yMin: 0, yMax: 1 },
        { key: "Autocorrelation", title: "Автокорреляция", yLabel: "Коэффициент автокорреляции", yMin: -0.2, yMax: 0.2 },
        { key: "MutualInformation", title: "Взаимная информация", yLabel: "Взаимная информация", yMin: 0, yMax: 1 }
    ];

    // 5. Отрисовка графиков
    function renderSuite(suite) {

        const testsMap = {
            diff: {tests: DIFF_TESTS},
            nist: { tests: NIST_TESTS, color: { line: "#2ca02c", zone: "rgba(44,160,44,0.25)" } },
            diehard: { tests: DIEHARD_TESTS, color: { line: "#9467bd", zone: "rgba(148,103,189,0.25)" } },
            testu01: { tests: TESTU01_TESTS, color: { line: "#8c564b", zone: "rgba(140,86,75,0.25)" } },
            additional: { tests: ADDITIONAL_TESTS, color: { line: "#ff7f0e", zone: "rgba(255,127,14,0.25)" } }
        };

        const config = testsMap[suite];
        if (!config) return;

        const container = document.getElementById(`${suite}Charts`);
        if (!container) return;

        container.innerHTML = "";

        config.tests.forEach(test => {
            const col = document.createElement("div");
            col.className = "col-lg-6";

            const card = document.createElement("div");
            card.className = "card analysis-card";

            const body = document.createElement("div");
            body.className = "card-body";

            const canvasId = `${suite}-${test.key}`;

            const chartContainer = document.createElement("div");
            chartContainer.className = "chart-container";

            const canvas = document.createElement("canvas");
            canvas.id = canvasId;

            chartContainer.appendChild(canvas);
            body.appendChild(chartContainer);

            card.appendChild(body);
            col.appendChild(card);
            container.appendChild(col);

            const s = series(test.key);
            drawChart(
                canvasId,
                test.title,
                test.yLabel,
                s.mean, s.upper, s.lower,
                test.color || config.color,
                test.yMin,
                test.yMax,
                test.key
            );
        });
    }

    // 6. Управление переключением suite
    function showSuite(suite) {
        document.querySelectorAll(".suite-group").forEach(group => {
            group.style.display = (group.dataset.suite === suite) ? "block" : "none";
        });

        const url = new URL(window.location.href);
        url.searchParams.set("suite", suite);
        window.history.replaceState({}, "", url);

        renderSuite(suite);
    }

    // Инициализация
    let currentSuite = (new URL(window.location.href).searchParams.get("suite") || "diff").toLowerCase();

    const initialRadio = document.querySelector(`input[name="suiteRadio"][value="${currentSuite}"]`);
    if (initialRadio) initialRadio.checked = true;

    showSuite(currentSuite);

    document.querySelectorAll('input[name="suiteRadio"]').forEach(radio => {
        radio.addEventListener("change", function () {
            currentSuite = this.value;
            showSuite(currentSuite);
        });
    });

    // Смена алгоритма
    const algorithmSelect = document.getElementById("algorithmSelect");
    if (algorithmSelect) {
        algorithmSelect.addEventListener("change", function () {
            let form = this.closest("form");
            if (!form) {
                form = document.createElement("form");
                form.method = "get";
                form.action = window.location.pathname;
                form.style.display = "none";
                document.body.appendChild(form);

                const params = new URLSearchParams(window.location.search);
                for (const [key, value] of params) {
                    if (key !== "algorithm") {
                        const input = document.createElement("input");
                        input.type = "hidden";
                        input.name = key;
                        input.value = value;
                        form.appendChild(input);
                    }
                }
            }

            let input = form.querySelector('input[name="algorithm"]');
            if (!input) {
                input = document.createElement("input");
                input.type = "hidden";
                input.name = "algorithm";
                form.appendChild(input);
            }
            input.value = this.value;

            form.submit();
        });
    }
});