document.addEventListener("DOMContentLoaded", function () {
    const canvas = document.getElementById("compareChart");
    if (!canvas) return;

    if (typeof algorithms === "undefined" || typeof mean === "undefined") return;

    let chart = null;

    // Локальные данные для графика (их можно менять после fetch)
    let algorithmsData = algorithms;
    let meanData = mean;

    const DEFAULT_METRIC = {
        diff: "SAC",
        nist: "Monobit",
        diehard: "BirthdaySpacings",
        testu01: "Collision"
    };

    const TEST_SUITES = {
        diff: {
            label: "SAC / BIC",
            tests: {
                SAC: { title: "Avalanche Effect (SAC)", yLabel: "Доля изменённых битов", isPValue: false },
                BIC: { title: "Bit Independence Criterion (BIC)", yLabel: "Максимальная корреляция", isPValue: false }
            }
        },
        nist: {
            label: "NIST",
            tests: {
                Monobit: { title: "Monobit Test", isPValue: true },
                FrequencyWithinBlock: { title: "Frequency Within Block", isPValue: true },
                Runs: { title: "Runs Test", isPValue: true },
                LongestRunOfOnes: { title: "Longest Run of Ones", isPValue: true },
                BinaryMatrixRank: { title: "Binary Matrix Rank", isPValue: true },
                DiscreteFourier: { title: "Discrete Fourier Transform", isPValue: true },
                NonOverlappingTemplate: { title: "Non-overlapping Template", isPValue: true },
                OverlappingTemplate: { title: "Overlapping Template", isPValue: true },
                MaurerUniversal: { title: "Maurer Universal", isPValue: true },
                LempelZiv: { title: "Lempel-Ziv", isPValue: true },
                LinearComplexity: { title: "Linear Complexity", isPValue: true },
                Serial: { title: "Serial Test", isPValue: true },
                ApproximateEntropy: { title: "Approximate Entropy", isPValue: true },
                Cusum: { title: "Cumulative Sums", isPValue: true },
                RandomExcursions: { title: "Random Excursions", isPValue: true },
                RandomExcursionsVariant: { title: "Random Excursions Variant", isPValue: true }
            }
        },
        diehard: {
            label: "Diehard",
            tests: {
                BirthdaySpacings: { title: "Birthday Spacings", isPValue: true },
                CountOnes: { title: "Count Ones", isPValue: true },
                MatrixRanks: { title: "Matrix Ranks", isPValue: true },
                OverlappingPermutations: { title: "Overlapping Permutations", isPValue: true },
                RunsDiehard: { title: "Runs Test", isPValue: true },
                GcdDiehard: { title: "GCD Test", isPValue: true },
                SqueezeDiehard: { title: "Squeeze Test", isPValue: true },
                CrapsDiehard: { title: "Craps Test", isPValue: true }
            }
        },
        testu01: {
            label: "TestU01",
            tests: {
                Collision: { title: "Collision", isPValue: true },
                Gap: { title: "Gap", isPValue: true },
                Autocorrelation: { title: "Autocorrelation", isPValue: true },
                Spectral: { title: "Spectral", isPValue: true },
                HammingWeight: { title: "Hamming Weight", isPValue: true },
                SerialTest: { title: "Serial Test", isPValue: true },
                MultinomialTest: { title: "Multinomial Test", isPValue: true },
                ClosePairs: { title: "Close Pairs", isPValue: true },
                CouponCollector: { title: "Coupon Collector", isPValue: true }
            }
        }
    };

    const metricSelect = document.getElementById("metricSelect");
    if (!metricSelect) return;

    // rounds нужен для fetch
    const roundsInput = document.getElementById("roundsInput");

    // ---------- helpers ----------
    function getParams() {
        return new URLSearchParams(window.location.search);
    }

    // Меняем URL через location => reload
    function replaceUrlReload(params) {
        window.location.href = `${window.location.pathname}?${params.toString()}`;
    }

    // Новый способ: меняем URL без reload
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

        // если URL “грязный” — исправим
        let changed = false;
        if ((params.get("suite") || "").toLowerCase() !== s) { params.set("suite", s); changed = true; }

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

        metricSelect.value = metric;
    }

    function drawChart(suite, metric) {
        const suiteCfg = TEST_SUITES[suite];
        const testCfg = suiteCfg.tests[metric];
        if (!testCfg) return;

        if (chart) {
            chart.destroy(); // обязателен перед переиспользованием canvas
            chart = null;
        }

        const colors = meanData.map(v =>
            testCfg.isPValue && v < 0.01 ? "#d62728" : "#1f77b4"
        );

        chart = new Chart(canvas, {
            type: "bar",
            data: {
                labels: algorithmsData,
                datasets: [{
                    label: "Среднее значение",
                    data: meanData,
                    backgroundColor: colors
                }]
            },
            options: {
                plugins: {
                    legend: { display: false },
                    title: {
                        display: true,
                        text: `Сравнение алгоритмов — ${suiteCfg.label}: ${testCfg.title}`
                    },
                    tooltip: {
                        displayColors: false,
                        callbacks: {
                            label: (ctx) => {
                                const v = ctx.parsed.y;
                                return testCfg.isPValue
                                    ? (v < 0.01 ? `p-value: ${v.toExponential(2)} (FAIL)` : `p-value: ${v.toFixed(4)}`)
                                    : `Значение: ${v.toFixed(6)}`;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,

                        // И SAC, и p-value в диапазоне 0..1
                        min: 0,
                        max: 1,

                        ticks: {
                            stepSize: 0.1,
                            callback: (v) => Number(v).toFixed(1).replace(".", ",")
                        },

                        title: { display: true, text: testCfg.isPValue ? "p-value" : testCfg.yLabel }
                    }
                }
            }
        });
    }

    // Загрузка данных без перезагрузки ---
    async function fetchCompareData(suite, metric, rounds) {
        // Поменяй путь, если у тебя другой action/route
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

        // при смене suite — ДА, перегружаем (как у тебя)
        params.delete("metric");
        params.set("metric", m);

        replaceUrlReload(params);

        // ниже строки фактически не успеют отработать из-за reload, но оставим
        syncSuiteRadios(s);
        fillMetricSelect(s, m);
        drawChart(s, m);
    }

    // ИЗМЕНЕНО: теперь без reload, с fetch + redraw
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

        // Подтянем новые значения и перерисуем график
        try {
            const data = await fetchCompareData(s, m, getRounds());

            // ожидаем { algorithms: [...], mean: [...] }
            if (data?.algorithms && data?.mean) {
                algorithmsData = data.algorithms;
                meanData = data.mean;
            }

            fillMetricSelect(s, m);
            drawChart(s, m);
        } catch (e) {
            console.error(e);
            // если запрос упал — хотя бы оставим выбранное в селекте
            fillMetricSelect(s, m);
        }
    }

    // ---------- init ----------
    const initState = normalizeStateFromUrl();
    syncSuiteRadios(initState.suite);
    fillMetricSelect(initState.suite, initState.metric);
    drawChart(initState.suite, initState.metric);

    // ---------- listeners ----------
    metricSelect.addEventListener("change", () => {
        // важно: async, без submit/reload
        setMetric(metricSelect.value);
    });

    document.querySelectorAll("input[name=suite]").forEach(radio => {
        radio.addEventListener("change", (e) => {
            setSuite(e.target.value);
        });
    });
});
