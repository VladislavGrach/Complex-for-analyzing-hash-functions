document.addEventListener("DOMContentLoaded", function () {
    const canvas = document.getElementById("compareChart");
    if (!canvas) return;

    // Получаем данные из глобальных переменных, установленных в представлении
    const algorithms = window.initialAlgorithms || [];
    const mean = window.initialMean || [];

    let chart = null;

    // Локальные данные для графика (их можно менять после fetch)
    let algorithmsData = algorithms;
    let meanData = mean;

    const DEFAULT_METRIC = {
        diff: "SAC",
        nist: "Monobit",
        diehard: "BirthdaySpacings",
        testu01: "Collision",
        additional: "ChiSquare"
    };

    const TEST_SUITES = {
        diff: {
            label: "SAC / BIC",
            tests: {
                SAC: { title: "Строгий лавинный критерий (SAC)", yLabel: "Доля изменённых битов", isPValue: false },
                BIC: { title: "Критерий независимости битов (BIC)", yLabel: "Максимальная корреляция", isPValue: false }
            }
        },
        nist: {
            label: "NIST",
            tests: {
                Monobit: { title: "Частотный побитовый тест", isPValue: true },
                FrequencyWithinBlock: { title: "Частотный блочный тест", isPValue: true },
                Runs: { title: "Тест на последовательность одинаковых бит", isPValue: true },
                LongestRunOfOnes: { title: "Тест на самую длинную последовательность единиц в блоке", isPValue: true },
                BinaryMatrixRank: { title: "Тест рангов бинарных матриц", isPValue: true },
                DiscreteFourier: { title: "Спектральный тест", isPValue: true },
                NonOverlappingTemplate: { title: "Тест на совпадение непересекающихся шаблонов", isPValue: true },
                OverlappingTemplate: { title: "Тест на совпадение пересекающихся шаблонов", isPValue: true },
                MaurerUniversal: { title: "Универсальный статистический тест Маурера ", isPValue: true },
                LempelZiv: { title: "Тест сжатия Лемпеля-Зива", isPValue: true },
                LinearComplexity: { title: "Тест на линейную сложность", isPValue: true },
                Serial: { title: "Тест на периодичность", isPValue: true },
                ApproximateEntropy: { title: "Тест приближённой энтропии", isPValue: true },
                Cusum: { title: "Тест кумулятивных сумм", isPValue: true },
                RandomExcursions: { title: "Тест случайных блужданий", isPValue: true },
                RandomExcursionsVariant: { title: "Модифицированный тест случайных блужданий", isPValue: true }
            }
        },
        diehard: {
            label: "Diehard",
            tests: {
                BirthdaySpacings: { title: "Тест распределения дней рождения", isPValue: true },
                CountOnes: { title: "Тест подсчёта единиц", isPValue: true },
                MatrixRanks: { title: "Тест рангов матриц", isPValue: true },
                OverlappingPermutations: { title: "Тест на пересекающиеся перестановки", isPValue: true },
                RunsDiehard: { title: "Тест на серии", isPValue: true },
                Gcd: { title: "Тест наибольшего общего делителя", isPValue: true },
                Squeeze: { title: "Тест сжатия", isPValue: true },
                Craps: { title: "Тест игры в крэпс", isPValue: true }
            }
        },
        testu01: {
            label: "TestU01",
            tests: {
                Collision: { title: "Тест на коллизии", isPValue: true },
                Gap: { title: "Тест на промежутки", isPValue: true },
                Autocorrelation: { title: "Тест автокорреляции", isPValue: true },
                Spectral: { title: "Спектральный тест", isPValue: true },
                HammingWeight: { title: "Тест веса Хэмминга", isPValue: true },
                SerialTest: { title: "Серийный тест", isPValue: true },
                MultinomialTest: { title: "Мультиномиальный тест", isPValue: true },
                ClosePairs: { title: "Тест близких пар", isPValue: true },
                CouponCollector: { title: "Тест коллекционера купонов", isPValue: true }
            }
        },
        additional: {
            label: "Статистические характеристики",
            tests: {
                ChiSquare: {
                    title: "Критерий χ²",
                    yLabel: "Значение χ²",
                    isPValue: false,
                    yMin: 0,
                    yMax: null // авто-масштабирование
                },
                ShannonEntropy: {
                    title: "Энтропия Шеннона",
                    yLabel: "Энтропия",
                    isPValue: false,
                    yMin: 0,
                    yMax: 1
                },
                Autocorrelation: {
                    title: "Автокорреляция",
                    yLabel: "Коэффициент автокорреляции",
                    isPValue: false,
                    yMin: -1,
                    yMax: 1
                },
                MutualInformation: {
                    title: "Взаимная информация",
                    yLabel: "Взаимная информация",
                    isPValue: false,
                    yMin: 0,
                    yMax: 1
                }
            }
        }
    };

    const metricSelect = document.getElementById("metricSelect");
    if (!metricSelect) return;

    const roundsInput = document.getElementById("roundsInput");

    // ---------- helpers ----------
    function getParams() {
        return new URLSearchParams(window.location.search);
    }

    // Меняем URL через location => reload
    function replaceUrlReload(params) {
        window.location.href = `${window.location.pathname}?${params.toString()}`;
    }

    // Меняем URL без reload
    function replaceUrlNoReload(params) {
        const url = new URL(window.location.href);
        url.search = params.toString();
        history.replaceState(null, "", url);
    }

    function getCheckedSuite() {
        const el = document.querySelector("input[name=suite]:checked");
        return (el?.value || "diff").toLowerCase();
    }

    function getRounds() {
        const v = roundsInput ? parseInt(roundsInput.value || "8", 10) : 8;
        return Number.isFinite(v) ? v : 8;
    }

    function normalizeStateFromUrl() {
        const params = getParams();

        let s = (params.get("suite") || getCheckedSuite() || "diff").toLowerCase();
        if (!TEST_SUITES[s]) s = "diff";

        let m = params.get("metric") || DEFAULT_METRIC[s];
        if (!TEST_SUITES[s].tests[m]) m = DEFAULT_METRIC[s];

        // если URL "грязный" — исправим
        let changed = false;
        if ((params.get("suite") || "").toLowerCase() !== s) {
            params.set("suite", s);
            changed = true;
        }

        if (params.get("metric") !== m) {
            params.delete("metric");
            params.set("metric", m);
            changed = true;
        }

        if (changed) replaceUrlReload(params);

        return { suite: s, metric: m };
    }

    function syncSuiteRadios(suite) {
        document.querySelectorAll("input[name=suite]").forEach(r => {
            r.checked = (r.value.toLowerCase() === suite);
        });
    }

    function fillMetricSelect(suite, metric) {
        metricSelect.innerHTML = "";
        const tests = TEST_SUITES[suite].tests;

        for (const key in tests) {
            const opt = document.createElement("option");
            opt.value = key;
            opt.textContent = tests[key].title;
            metricSelect.appendChild(opt);
        }

        if (metric && TEST_SUITES[suite].tests[metric]) {
            metricSelect.value = metric;
        } else {
            metricSelect.value = DEFAULT_METRIC[suite];
        }
    }

    function drawChart(suite, metric) {
        const suiteCfg = TEST_SUITES[suite];
        const testCfg = suiteCfg.tests[metric];
        if (!testCfg) return;

        // Определяем границы оси Y на основе конфигурации теста
        const yMin = testCfg.yMin !== undefined ? testCfg.yMin : 0;
        const yMax = testCfg.yMax !== undefined ? testCfg.yMax : 1;

        // Для авто-масштабирования (yMax = null) используем максимальное значение из данных
        let effectiveYMax = yMax;
        if (yMax === null && meanData.length > 0) {
            // Находим максимальное значение среди данных и добавляем 10% запаса
            const maxValue = Math.max(...meanData.filter(v => v !== null && !isNaN(v)));
            effectiveYMax = Math.ceil(maxValue * 1.1); // +10% и округляем вверх
        }

        // Если график уже существует — обновляем его
        if (chart) {

            chart.data.labels = algorithmsData;

            chart.data.datasets[0].data = meanData;

            chart.data.datasets[0].label =
                testCfg.isPValue
                    ? "p-value" : testCfg.yLabel;

            chart.data.datasets[0].backgroundColor = meanData.map(v =>
                testCfg.isPValue && v < 0.01
                    ? "#d62728"
                    : "#1f77b4"
            );

            // Заголовок
            chart.options.plugins.title.text =
                `${suiteCfg.label}: ${testCfg.title}`;

            // Ось Y
            chart.options.scales.y.title.text =
                testCfg.isPValue
                    ? "p-value"
                    : testCfg.yLabel;

            chart.options.scales.y.min = yMin;
            chart.options.scales.y.max = effectiveYMax;

            // ticks
            if (effectiveYMax > 10) {
                chart.options.scales.y.ticks = {
                    callback: (v) =>
                        Number.isInteger(v) ? v : v.toFixed(0),
                    stepSize: Math.ceil(effectiveYMax / 10)
                };
            }
            else if (effectiveYMax <= 1) {
                chart.options.scales.y.ticks = {
                    stepSize: 0.1,
                    callback: (v) => v.toFixed(1)
                };
            }
            else {
                chart.options.scales.y.ticks = {
                    callback: (v) => v.toFixed(2)
                };
            }

            chart.update();

            return;
        }

        // Создаем новый график только если его еще нет
        const colors = meanData.map(v =>
            testCfg.isPValue && v < 0.01 ? "#d62728" : "#1f77b4"
        );

        // Настраиваем опции для оси Y
        const yAxisOptions = {
            beginAtZero: yMin >= 0,
            min: yMin,
            max: effectiveYMax,
            title: {
                display: true,
                text: testCfg.isPValue ? "p-value" : testCfg.yLabel
            }
        };

        // Добавляем настройки ticks в зависимости от диапазона
        if (effectiveYMax > 10) {
            // Для больших значений (Chi-Square) используем целые числа
            yAxisOptions.ticks = {
                callback: (v) => Number.isInteger(v) ? v : v.toFixed(0),
                stepSize: Math.ceil(effectiveYMax / 10) // Примерно 10 делений
            };
        } else if (effectiveYMax <= 1) {
            // Для значений от 0 до 1
            yAxisOptions.ticks = {
                stepSize: 0.1,
                callback: (v) => v.toFixed(1)
            };
        } else {
            // Для промежуточных значений
            yAxisOptions.ticks = {
                callback: (v) => v.toFixed(2)
            };
        }

        chart = new Chart(canvas, {
            type: "bar",
            data: {
                labels: algorithmsData,
                datasets: [{
                    label: testCfg.isPValue ? "p-value" : testCfg.yLabel,
                    data: meanData,
                    backgroundColor: colors,
                    borderRadius: 6,
                    barPercentage: 0.7
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    /*legend: { display: false },*/
                    legend: {
                        position: "bottom",
                        labels: {
                            usePointStyle: true,
                            pointStyle: "circle"
                        }
                    },
                    title: {
                        display: true,
                        text: `Сравнение алгоритмов — ${suiteCfg.label}: ${testCfg.title}`,
                        font: { size: 14, weight: '500' }
                    },
                    tooltip: {
                        displayColors: false,
                        callbacks: {
                            label: (ctx) => {
                                const v = ctx.parsed.y;
                                if (testCfg.isPValue) {
                                    return v < 0.01
                                        ? `p-value: ${v.toExponential(2)} (ТЕСТ НЕ ПРОЙДЕН)`
                                        : `p-value: ${v.toFixed(6)}`;
                                } else {
                                    return `Значение: ${v.toFixed(6)}`;
                                }
                            }
                        }
                    }
                },
                scales: {
                    y: yAxisOptions,
                    x: {
                        ticks: {
                            rotation: 0,
                            maxRotation: 0,
                            minRotation: 0,
                            autoSkip: false,
                            align: "center",
                            font: { size: 11 }
                        }
                    }
                },
                animation: true
            },
            devicePixelRatio: 2
        });
    }

    // Загрузка данных без перезагрузки
    async function fetchCompareData(suite, metric, rounds) {
        const url = new URL("/HashAnalysis/CompareData", window.location.origin);
        url.searchParams.set("suite", suite);
        url.searchParams.set("metric", metric);
        url.searchParams.set("rounds", rounds);

        const resp = await fetch(url, { headers: { "Accept": "application/json" } });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        return await resp.json();
    }

    function setSuite(newSuite) {
        const s = (newSuite || "diff").toLowerCase();
        const m = DEFAULT_METRIC[s];

        const params = getParams();
        params.set("suite", s);
        params.delete("metric");
        params.set("metric", m);

        replaceUrlReload(params);
    }

    // Асинхронная смена метрики без перезагрузки
    async function setMetric(newMetric) {
        const s = getCheckedSuite();
        const m = newMetric;

        if (!TEST_SUITES[s]?.tests[m]) return;

        const params = getParams();
        params.set("suite", s);
        params.set("rounds", String(getRounds()));
        params.delete("metric");
        params.set("metric", m);

        // URL обновляем без перезагрузки
        replaceUrlNoReload(params);

        // Показываем индикатор загрузки
        metricSelect.style.opacity = "0.6";
        metricSelect.disabled = true;

        try {
            const data = await fetchCompareData(s, m, getRounds());

            if (data?.algorithms && data?.mean) {
                algorithmsData = data.algorithms;
                meanData = data.mean;
            }

            fillMetricSelect(s, m);
            drawChart(s, m);
        } catch (e) {
            console.error("Ошибка загрузки данных:", e);
            fillMetricSelect(s, m);
        } finally {
            metricSelect.style.opacity = "1";
            metricSelect.disabled = false;
        }
    }

    // ---------- init ----------
    const initState = normalizeStateFromUrl();

    syncSuiteRadios(initState.suite);
    fillMetricSelect(initState.suite, initState.metric);

    // INITIAL FETCH
    (async () => {
        try {
            const data = await fetchCompareData(
                initState.suite,
                initState.metric,
                getRounds()
            );

            if (data?.algorithms && data?.mean) {
                algorithmsData = data.algorithms;
                meanData = data.mean;
            }

            drawChart(initState.suite, initState.metric);
        }
        catch (e) {
            console.error("Ошибка initial load:", e);
        }
    })();

    // ---------- listeners ----------
    metricSelect.addEventListener("change", () => {
        setMetric(metricSelect.value);
    });

    document.querySelectorAll("input[name=suite]").forEach(radio => {
        radio.addEventListener("change", (e) => {
            setSuite(e.target.value);
        });
    });

    if (roundsInput) {
        roundsInput.addEventListener("change", () => {
            const params = getParams();
            params.set("rounds", roundsInput.value);
            replaceUrlReload(params);
        });
    }

    // Загрузка локализации тестов
    const localizationElement = document.getElementById('test-localization-data');
    const testLocalization = localizationElement ? JSON.parse(localizationElement.textContent) : {};

    // Функция получения русского названия теста
    function getLocalizedTestName(suite, metricKey) {
        if (!testLocalization) return metricKey;

        const suiteMap = {
            'nist': testLocalization.nist,
            'diehard': testLocalization.diehard,
            'testu01': testLocalization.testu01,
            'additional': testLocalization.additional
        };

        if (suite === 'diff') {
            if (metricKey === 'SAC') {
                return testLocalization.sac?.['MeanFlipRate'] || "Строгий лавинный критерий";
            }
            if (metricKey === 'BIC') {
                return testLocalization.bic?.['MaxCorrelationAbs'] || "Критерий независимости битов";
            }
            return metricKey;
        }

        const map = suiteMap[suite];
        if (map && map[metricKey]) {
            return map[metricKey];
        }
        return metricKey;
    }

    // Обработчики для кнопок экспорта
    document.getElementById("exportCsvBtn")?.addEventListener("click", () => {
        const rounds = getRounds();
        const suite = getCheckedSuite();
        const metric = metricSelect.value;

        // Получаем русское название теста
        const metricName = getLocalizedTestName(suite, metric);

        // Формируем CSV
        let csv = "Алгоритм;Значение\n";
        for (let i = 0; i < algorithmsData.length; i++) {
            csv += `${algorithmsData[i]};${meanData[i]}\n`;
        }
        // Заменяем точку на запятую для Excel
        csv = csv.replace(/\./g, ',');

        // Скачиваем файл
        const blob = new Blob(["\uFEFF" + csv], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Сравнение. ${metricName} ${rounds} раундов.csv`;
        a.click();
        URL.revokeObjectURL(url);
    });

    document.getElementById("exportJsonBtn")?.addEventListener("click", () => {
        const rounds = getRounds();
        const suite = getCheckedSuite();
        const metric = metricSelect.value;

        // Получаем русское название теста
        const metricName = getLocalizedTestName(suite, metric);

        // Формируем JSON
        const data = {
            "Набор тестов": suite === 'diff' ? "Дифференциальные тесты" : suite,
            "Тест": metricName,
            "Количество раундов": rounds,
            "Алгоритмы": algorithmsData,
            "Значения": meanData
        };

        // Скачиваем файл
        const blob = new Blob(
            [JSON.stringify(data, null, 2)],
            { type: 'application/json' }
        );

        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `Сравнение. ${metricName} ${rounds} раундов.json`;
        a.click();
        URL.revokeObjectURL(url);
    });

    document.getElementById("exportPngBtn")?.addEventListener("click", () => {
        if (!chart) return;

        const rounds = getRounds();
        const suite = getCheckedSuite();
        const metric = metricSelect.value;

        // Получаем русское название теста
        const metricName = getLocalizedTestName(suite, metric);

        const canvas = chart.canvas;
        const ctx = canvas.getContext("2d");

        // Сохраняем текущее состояние
        ctx.save();

        // Белый фон под графиком
        ctx.globalCompositeOperation = "destination-over";
        ctx.fillStyle = "#ffffff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        // Получаем изображение
        const url = canvas.toDataURL("image/png");

        // Восстанавливаем состояние
        ctx.restore();

        const a = document.createElement("a");
        a.href = url;
        a.download = `Сравнение. ${metricName} ${rounds} раундов.png`;
        a.click();
    });
});