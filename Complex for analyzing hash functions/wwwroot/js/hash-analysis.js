document.addEventListener("DOMContentLoaded", function () {
    const data = window.hashAnalysisData;
    if (!data || !data.rounds || !data.metrics) return;

    const rounds = data.rounds;
    const metricsArr = data.metrics; // массив объектов: { "SAC": {Mean, Ci?, Upper?, Lower?, ...}, "BIC": {...}, ... }

    // 1. Функция извлечения серии (mean + границы)
    function series(name) {
        const mean = metricsArr.map(m => (m && m[name] ? m[name].Mean : null));

        // Предпочитаем серверные Upper/Lower, если они есть
        const upperFromServer = metricsArr.map(m => (m && m[name] ? (m[name].Upper ?? null) : null));
        const lowerFromServer = metricsArr.map(m => (m && m[name] ? (m[name].Lower ?? null) : null));

        const hasBounds = upperFromServer.some(v => v !== null) && lowerFromServer.some(v => v !== null);

        if (hasBounds) {
            return { mean, upper: upperFromServer, lower: lowerFromServer };
        }

        // Fallback: mean ± Ci
        const ci = metricsArr.map(m => (m && m[name] ? (m[name].Ci ?? 0) : 0));
        const upper = mean.map((v, i) => (v == null ? null : v + ci[i]));
        const lower = mean.map((v, i) => (v == null ? null : v - ci[i]));

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

    const snapTooltipToMeanPlugin = {
        id: 'snapTooltipToMean',
        afterEvent(chart, args) {
            const e = args.event;
            if (!e || !chart.tooltip) return;
            if (e.type !== 'mousemove') return;

            const meanVisible = chart.isDatasetVisible(2);
            const ciVisible = chart.isDatasetVisible(1);

            if (!meanVisible && !ciVisible) {
                chart.setActiveElements([]);
                chart.tooltip.setActiveElements([], { x: 0, y: 0 });
                chart.draw();
                return;
            }

            const points = chart.getElementsAtEventForMode(e, 'index', { intersect: false }, false);
            if (!points || points.length === 0) {
                chart.setActiveElements([]);
                chart.tooltip.setActiveElements([], { x: 0, y: 0 });
                chart.draw();
                return;
            }

            const index = points[0].index;
            const active = [{ datasetIndex: 2, index }];
            chart.setActiveElements(active);
            chart.tooltip.setActiveElements(active, e);
            chart.draw();
        }
    };

    // 3. Универсальная функция отрисовки графика + кнопки экспорта
    function drawChart(id, title, yLabel, mean, upper, lower, color, yMin = null, yMax = null, testKey = null) {
        const canvas = document.getElementById(id);
        if (!canvas) return;

        const chart = new Chart(canvas, {
            type: 'line',
            plugins: [whiteBackgroundPlugin, snapTooltipToMeanPlugin],
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
                                const meanVisible = chart.isDatasetVisible(2);
                                const ciVisible = chart.isDatasetVisible(1);
                                if (!meanVisible && !ciVisible) return null;
                                return `Количество раундов: ${items[0].label}`;
                            },
                            beforeBody: function (items) {
                                const chart = items[0]?.chart;
                                if (!chart) return [];
                                const meanVisible = chart.isDatasetVisible(2);
                                const ciVisible = chart.isDatasetVisible(1);
                                if (!meanVisible && !ciVisible) return [];
                                const i = items[0].dataIndex;
                                const upperVal = chart.data.datasets[0].data[i];
                                const lowerVal = chart.data.datasets[1].data[i];
                                const meanVal = chart.data.datasets[2].data[i];
                                const fmt = (v) => Number(v).toFixed(6).replace('.', ',');
                                const lines = [];
                                if (meanVisible && meanVal != null) lines.push(`Среднее значение: ${fmt(meanVal)}`);
                                if (ciVisible && upperVal != null && lowerVal != null) {
                                    const eps = 1e-12;
                                    const same = Math.abs(Number(upperVal) - Number(lowerVal)) < eps;
                                    if (same) lines.push(`Граница: ${fmt(upperVal)}`);
                                    else {
                                        lines.push(`Верхняя граница: ${fmt(upperVal)}`);
                                        lines.push(`Нижняя граница: ${fmt(lowerVal)}`);
                                    }
                                }
                                return lines;
                            },
                            label: function () { return ''; },
                            labelColor: function (ctx) {
                                const chart = ctx.chart;
                                const meanVisible = chart.isDatasetVisible(2);
                                const ciVisible = chart.isDatasetVisible(1);
                                if (!meanVisible && !ciVisible) {
                                    return { borderColor: 'rgba(0,0,0,0)', backgroundColor: 'rgba(0,0,0,0)' };
                                }
                                if (meanVisible) {
                                    const dsMean = chart.data.datasets[2];
                                    const c = dsMean.backgroundColor || dsMean.borderColor;
                                    return { borderColor: c, backgroundColor: c };
                                }
                                const dsCi = chart.data.datasets[1];
                                return { borderColor: 'rgba(0,0,0,0)', backgroundColor: dsCi.backgroundColor };
                            }
                        }
                    }
                },
                scales: {
                    x: { title: { display: true, text: 'Число раундов' } },
                    y: { min: yMin, max: yMax, title: { display: true, text: yLabel }, ticks: { stepSize: 0.2 } }
                }
            }
        });

        // Кнопки экспорта под графиком
        const btnGroup = document.createElement("div");
        btnGroup.className = "d-flex gap-2 mt-3 flex-wrap";

        const csvBtn = document.createElement("button");
        csvBtn.className = "btn btn-outline-secondary btn-sm";
        csvBtn.textContent = "Экспорт CSV";
        csvBtn.onclick = () => {
            let csv = "Раунд,Среднее,Верхняя граница,Нижняя граница\n";
            rounds.forEach((r, i) => {
                csv += `${r},${mean[i] ?? ""},${upper[i] ?? ""},${lower[i] ?? ""}\n`;
            });
            const blob = new Blob([csv], { type: "text/csv;charset=utf-8;" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = `${(testKey || title).replace(/\s+/g, "_")}_${window.currentAlgorithm}.csv`;
            a.click();
            URL.revokeObjectURL(url);
        };

        const jsonBtn = document.createElement("button");
        jsonBtn.className = "btn btn-outline-secondary btn-sm";
        jsonBtn.textContent = "Экспорт JSON";
        jsonBtn.onclick = () => {
            const exportData = {
                algorithm: window.currentAlgorithm,
                suite: document.querySelector('input[name="suiteRadio"]:checked')?.value || 'diff',
                test: testKey || title,
                rounds: rounds,
                mean: mean,
                upper: upper,
                lower: lower
            };
            const blob = new Blob([JSON.stringify(exportData, null, 2)], { type: "application/json" });
            const url = URL.createObjectURL(blob);
            const a = document.createElement("a");
            a.href = url;
            a.download = `${(testKey || title).replace(/\s+/g, "_")}_${window.currentAlgorithm}.json`;
            a.click();
            URL.revokeObjectURL(url);
        };

        const pngBtn = document.createElement("button");
        pngBtn.className = "btn btn-outline-secondary btn-sm";
        pngBtn.textContent = "Экспорт PNG";
        pngBtn.onclick = () => {
            const link = document.createElement("a");
            link.href = chart.toBase64Image();
            link.download = `${(testKey || title).replace(/\s+/g, "_")}_${window.currentAlgorithm}.png`;
            link.click();
        };

        btnGroup.appendChild(csvBtn);
        btnGroup.appendChild(jsonBtn);
        btnGroup.appendChild(pngBtn);

        canvas.parentElement.appendChild(btnGroup);
    }

    // 4. Константы тестов
    const COLORS = {
        SAC: { line: '#1f77b4', zone: 'rgba(31,119,180,0.25)' },
        BIC: { line: '#d62728', zone: 'rgba(214,39,40,0.25)' },
        default: { line: '#2ca02c', zone: 'rgba(44,160,44,0.25)' }
    };

    const NIST_TESTS = [
        { key: "Monobit", title: "Monobit Test", yMin: 0, yMax: 1 },
        { key: "FrequencyWithinBlock", title: "Frequency Within Block", yMin: 0, yMax: 1 },
        { key: "Runs", title: "Runs Test", yMin: 0, yMax: 1 },
        { key: "LongestRunOfOnes", title: "Longest Run of Ones", yMin: 0, yMax: 1 },
        { key: "BinaryMatrixRank", title: "Binary Matrix Rank", yMin: 0, yMax: 1 },
        { key: "DiscreteFourier", title: "Discrete Fourier Transform", yMin: 0, yMax: 1 },
        { key: "NonOverlappingTemplate", title: "Non-overlapping Template", yMin: 0, yMax: 1 },
        { key: "OverlappingTemplate", title: "Overlapping Template", yMin: 0, yMax: 1 },
        { key: "MaurerUniversal", title: "Maurer Universal", yMin: 0, yMax: 1 },
        { key: "LempelZiv", title: "Lempel-Ziv", yMin: 0, yMax: 1 },
        { key: "LinearComplexity", title: "Linear Complexity", yMin: 0, yMax: 1 },
        { key: "Serial", title: "Serial Test", yMin: 0, yMax: 1 },
        { key: "ApproximateEntropy", title: "Approximate Entropy", yMin: 0, yMax: 1 },
        { key: "Cusum", title: "Cumulative Sums", yMin: 0, yMax: 1 },
        { key: "RandomExcursions", title: "Random Excursions", yMin: 0, yMax: 1 },
        { key: "RandomExcursionsVariant", title: "Random Excursions Variant", yMin: 0, yMax: 1 }
    ];

    const DIEHARD_TESTS = [
        { key: "BirthdaySpacings", title: "Birthday Spacings Test", yMin: 0, yMax: 1 },
        { key: "CountOnes", title: "Count Ones Test", yMin: 0, yMax: 1 },
        { key: "MatrixRanks", title: "Matrix Ranks Test", yMin: 0, yMax: 1 },
        { key: "OverlappingPermutations", title: "Overlapping Permutations Test", yMin: 0, yMax: 1 },
        { key: "RunsDiehard", title: "Runs Test (Diehard)", yMin: 0, yMax: 1 },
        { key: "GcdDiehard", title: "GCD Test (Diehard)", yMin: 0, yMax: 1 },
        { key: "SqueezeDiehard", title: "Squeeze Test (Diehard)", yMin: 0, yMax: 1 },
        { key: "CrapsDiehard", title: "Craps Test (Diehard)", yMin: 0, yMax: 1 }
    ];

    const TESTU01_TESTS = [
        { key: "Collision", title: "Collision Test", yMin: 0, yMax: 1 },
        { key: "Gap", title: "Gap Test", yMin: 0, yMax: 1 },
        { key: "Autocorrelation", title: "Autocorrelation Test", yMin: 0, yMax: 1 },
        { key: "Spectral", title: "Spectral Test", yMin: 0, yMax: 1 },
        { key: "HammingWeight", title: "Hamming Weight Test", yMin: 0, yMax: 1 },
        { key: "SerialTest", title: "Serial Test (TestU01)", yMin: 0, yMax: 1 },
        { key: "MultinomialTest", title: "Multinomial Test", yMin: 0, yMax: 1 },
        { key: "ClosePairs", title: "Close Pairs Test", yMin: 0, yMax: 1 },
        { key: "CouponCollector", title: "Coupon Collector Test", yMin: 0, yMax: 1 }
    ];

    // 5. Отрисовка графиков для выбранного suite
    function renderSuite(suite) {
        if (suite === "diff") {
            const sac = series("SAC");
            drawChart(
                "sacChart",
                "Strict Avalanche Criterion (SAC)",
                "Доля изменённых выходных битов",
                sac.mean, sac.upper, sac.lower,
                COLORS.SAC,
                0, 1,
                "SAC"
            );

            const bic = series("BIC");
            drawChart(
                "bicChart",
                "Bit Independence Criterion (BIC)",
                "Максимальная корреляция",
                bic.mean, bic.upper, bic.lower,
                COLORS.BIC,
                -1.4, 2,
                "BIC"
            );
            return;
        }

        // Для NIST, Diehard, TestU01 — динамическая генерация
        const testsMap = {
            nist: { tests: NIST_TESTS, color: { line: "#2ca02c", zone: "rgba(44,160,44,0.25)" } },
            diehard: { tests: DIEHARD_TESTS, color: { line: "#9467bd", zone: "rgba(148,103,189,0.25)" } },
            testu01: { tests: TESTU01_TESTS, color: { line: "#8c564b", zone: "rgba(140,86,75,0.25)" } }
        };

        const config = testsMap[suite];
        if (!config) return;

        const container = document.getElementById(`${suite}Charts`);
        if (!container) return;

        container.innerHTML = ""; // очищаем перед перерисовкой

        config.tests.forEach(test => {
            const col = document.createElement("div");
            col.className = "col-lg-6";

            const card = document.createElement("div");
            card.className = "card shadow-sm";

            const body = document.createElement("div");
            body.className = "card-body";

            const h5 = document.createElement("h5");
            h5.className = "card-title";
            h5.textContent = test.title;

            const canvas = document.createElement("canvas");
            const canvasId = `${suite}-${test.key}`;
            canvas.id = canvasId;

            body.appendChild(h5);
            body.appendChild(canvas);
            card.appendChild(body);
            col.appendChild(card);
            container.appendChild(col);

            const s = series(test.key);
            drawChart(
                canvasId,
                test.title,
                "p-value",
                s.mean, s.upper, s.lower,
                config.color,
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

        // Обновляем URL
        const url = new URL(window.location.href);
        url.searchParams.set("suite", suite);
        window.history.replaceState({}, "", url);

        // Рисуем графики (важно — после показа блока!)
        renderSuite(suite);
    }

    // Инициализация при загрузке
    let currentSuite = (new URL(window.location.href).searchParams.get("suite") || "diff").toLowerCase();

    // Синхронизируем радио-кнопку
    const initialRadio = document.querySelector(`input[name="suiteRadio"][value="${currentSuite}"]`);
    if (initialRadio) initialRadio.checked = true;

    showSuite(currentSuite);

    // Слушатель переключения радио
    document.querySelectorAll('input[name="suiteRadio"]').forEach(radio => {
        radio.addEventListener("change", function () {
            currentSuite = this.value;
            showSuite(currentSuite);
        });
    });

    // 7. Смена алгоритма → отправка GET-запроса
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

                // Копируем текущие параметры (suite и т.д.)
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

            // Добавляем/обновляем algorithm
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