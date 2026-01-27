document.addEventListener("DOMContentLoaded", function () {
    const canvas = document.getElementById("compareChart");
    if (!canvas) return;

    if (typeof algorithms === "undefined" || typeof mean === "undefined") return;

    let chart = null;

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

    // ---------- helpers ----------
    function getParams() {
        return new URLSearchParams(window.location.search);
    }

    function replaceUrl(params) {
        window.location.href =
            `${window.location.pathname}?${params.toString()}`;
    }

    function getCheckedSuite() {
        const el = document.querySelector("input[name=suite]:checked");
        return (el?.value || "diff").toLowerCase();
    }

    function normalizeStateFromUrl() {
        const params = getParams();

        let s = (params.get("suite") || getCheckedSuite() || "diff").toLowerCase();
        if (!TEST_SUITES[s]) s = "diff";

        let m = params.get("metric") || DEFAULT_METRIC[s];
        if (!TEST_SUITES[s].tests[m]) m = DEFAULT_METRIC[s];

        // если URL “грязный” (например, suite=nist&metric=SAC) — исправим его сразу
        let changed = false;
        if ((params.get("suite") || "").toLowerCase() !== s) { params.set("suite", s); changed = true; }

        if (params.get("metric") !== m) {
            params.delete("metric");            // важно: сначала удалить [web:611]
            params.set("metric", m);
            changed = true;
        }

        if (changed) replaceUrl(params);

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
            chart.destroy(); // корректная очистка инстанса [web:620]
            chart = null;
        }

        const colors = mean.map(v =>
            testCfg.isPValue && v < 0.01 ? "#d62728" : "#1f77b4"
        );

        chart = new Chart(canvas, {
            type: "bar",
            data: {
                labels: algorithms,
                datasets: [{
                    label: "Среднее значение",
                    data: mean,
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
                        max: testCfg.isPValue ? 1 : undefined,
                        title: { display: true, text: testCfg.isPValue ? "p-value" : testCfg.yLabel }
                    }
                }
            }
        });
    }

    function setSuite(newSuite) {
        const s = (newSuite || "diff").toLowerCase();
        const m = DEFAULT_METRIC[s];

        const params = getParams();
        params.set("suite", s);

        // ключевой момент: metric всегда переопределяем дефолтом для новой suite
        params.delete("metric");     // [web:611]
        params.set("metric", m);

        replaceUrl(params);

        syncSuiteRadios(s);
        fillMetricSelect(s, m);
        drawChart(s, m);
    }

    function setMetric(newMetric) {
        const s = getCheckedSuite();
        const m = newMetric;

        if (!TEST_SUITES[s]?.tests[m]) return;

        const params = getParams();
        params.set("suite", s);        // на всякий случай
        params.delete("metric");       // [web:611]
        params.set("metric", m);

        replaceUrl(params);

        fillMetricSelect(s, m);
        drawChart(s, m);
    }

    // ---------- init ----------
    const initState = normalizeStateFromUrl();
    syncSuiteRadios(initState.suite);
    fillMetricSelect(initState.suite, initState.metric);
    drawChart(initState.suite, initState.metric);

    // ---------- listeners ----------
    metricSelect.addEventListener("change", () => {
        setMetric(metricSelect.value);
    });

    document.querySelectorAll("input[name=suite]").forEach(radio => {
        radio.addEventListener("change", (e) => {
            setSuite(e.target.value);
        });
    });
});