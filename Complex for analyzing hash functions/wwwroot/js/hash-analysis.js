document.addEventListener("DOMContentLoaded", function () {

    const data = window.hashAnalysisData;
    if (!data || !data.rounds || !data.metrics) return;

    const rounds = data.rounds;
    const metricsArr = data.metrics; // массив объектов: { "SAC": {Mean,Ci,...}, "BIC": {...}, ... }

    function series(name) {
        const mean = metricsArr.map(m => (m && m[name] ? m[name].Mean : null));

        // Новый правильный путь: сервер отдал готовые границы
        const upperFromServer = metricsArr.map(m => (m && m[name] ? (m[name].Upper ?? null) : null));
        const lowerFromServer = metricsArr.map(m => (m && m[name] ? (m[name].Lower ?? null) : null));

        const hasBounds = upperFromServer.some(v => v != null) && lowerFromServer.some(v => v != null);

        if (hasBounds) {
            return { mean, upper: upperFromServer, lower: lowerFromServer };
        }

        // Старый путь (fallback): mean ± Ci
        const ci = metricsArr.map(m => (m && m[name] ? (m[name].Ci ?? 0) : 0));

        const upper = mean.map((v, i) => (v == null ? null : v + (ci[i] ?? 0)));
        const lower = mean.map((v, i) => (v == null ? null : v - (ci[i] ?? 0)));

        return { mean, upper, lower };
    }


    // ===== плагины =====
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

            const points = chart.getElementsAtEventForMode(
                e,
                'index',
                { intersect: false },
                false
            );

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

    function drawChart(id, title, yLabel, mean, upper, lower, color, yMin = null, yMax = null) {
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

        const btn = document.createElement("button");
        btn.className = "btn btn-outline-secondary btn-sm mt-2";
        btn.innerText = "Скачать PNG";
        btn.onclick = () => {
            const link = document.createElement("a");
            link.href = chart.toBase64Image();
            link.download = `${title.replace(/\s+/g, "_")}.png`;
            link.click();
        };
        canvas.parentElement.appendChild(btn);
    }

    const COLORS = {
        SAC: { line: '#1f77b4', zone: 'rgba(31,119,180,0.25)' },
        BIC: { line: '#d62728', zone: 'rgba(214,39,40,0.25)' },
        Monobit: { line: '#2ca02c', zone: 'rgba(44,160,44,0.25)' }
    };

    const algorithmForm = document.getElementById("algorithmForm");
    const suiteHidden = document.getElementById("suiteHidden");

    if (algorithmForm && suiteHidden) {
        algorithmForm.addEventListener("submit", () => {
            const checkedSuite = document.querySelector('input[name="suiteRadio"]:checked');
            suiteHidden.value = checkedSuite ? checkedSuite.value : "diff";
        });
    }

    function activateSuite(value) {
        document.querySelectorAll(".suite-group")
            .forEach(g => g.classList.add("d-none"));

        const active = document.querySelector(`.suite-group[data-suite="${value}"]`);
        if (active) {
            active.classList.remove("d-none");
        }

        if (suiteHidden) {
            suiteHidden.value = value;
        }

        const url = new URL(window.location.href);
        url.searchParams.set("suite", value);
        window.history.replaceState({}, "", url);
    }

    // 1) при загрузке берём suite из URL (если нет — diff)
    const url = new URL(window.location.href);
    const initialSuite = (url.searchParams.get("suite") || "diff").toLowerCase();

    // выставим радио визуально (на случай если server-side checked не совпал)
    const initialRadio = document.querySelector(`input[name="suiteRadio"][value="${initialSuite}"]`);

    if (initialRadio) initialRadio.checked = true;

    activateSuite(initialSuite);

    // 2) слушаем правильное имя радиокнопок: name="suite"
    document.querySelectorAll('input[name="suiteRadio"]').forEach(radio => {
        radio.addEventListener("change", () => activateSuite(radio.value));
    });

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

    // Строим конкретные графики
    const sac = series("SAC");
    drawChart("sacChart", "Strict Avalanche Criterion (SAC)", "Доля изменённых выходных битов",
        sac.mean, sac.upper, sac.lower, COLORS.SAC, 0, 1);

    const bic = series("BIC");
    drawChart("bicChart", "Bit Independence Criterion (BIC)", "Максимальная корреляция",
        bic.mean, bic.upper, bic.lower, COLORS.BIC, -1.4, 2);

    const nistContainer = document.getElementById("nistCharts");

    NIST_TESTS.forEach(test => {
        const col = document.createElement("div");
        col.className = "col-lg-6";

        const card = document.createElement("div");
        card.className = "card shadow-sm";

        const body = document.createElement("div");
        body.className = "card-body";

        const h5 = document.createElement("h5");
        h5.className = "card-title";
        h5.innerText = test.title;

        const canvas = document.createElement("canvas");
        const canvasId = `nist-${test.key}`;
        canvas.id = canvasId;
        canvas.height = 140;

        body.appendChild(h5);
        body.appendChild(canvas);
        card.appendChild(body);
        col.appendChild(card);
        nistContainer.appendChild(col);

        const s = series(test.key);

        drawChart(
            canvasId,
            test.title,
            "p-value",
            s.mean,
            s.upper,
            s.lower,
            { line: "#2ca02c", zone: "rgba(44,160,44,0.25)" },
            test.yMin,
            test.yMax
        );
    });

    const diehardContainer = document.getElementById("diehardCharts");

    DIEHARD_TESTS.forEach(test => {
        const col = document.createElement("div");
        col.className = "col-lg-6";

        const card = document.createElement("div");
        card.className = "card shadow-sm";

        const body = document.createElement("div");
        body.className = "card-body";

        const h5 = document.createElement("h5");
        h5.className = "card-title";
        h5.innerText = test.title;

        const canvas = document.createElement("canvas");
        const canvasId = `diehard-${test.key}`;
        canvas.id = canvasId;
        canvas.height = 140;

        body.appendChild(h5);
        body.appendChild(canvas);
        card.appendChild(body);
        col.appendChild(card);
        diehardContainer.appendChild(col);

        const s = series(test.key);

        drawChart(
            canvasId,
            test.title,
            "p-value",
            s.mean,
            s.upper,
            s.lower,
            { line: "#9467bd", zone: "rgba(148,103,189,0.25)" },
            test.yMin,
            test.yMax
        );
    });

    const testu01Container = document.getElementById("testu01Charts");

    TESTU01_TESTS.forEach(test => {
        const col = document.createElement("div");
        col.className = "col-lg-6";

        const card = document.createElement("div");
        card.className = "card shadow-sm";

        const body = document.createElement("div");
        body.className = "card-body";

        const h5 = document.createElement("h5");
        h5.className = "card-title";
        h5.innerText = test.title;

        const canvas = document.createElement("canvas");
        const canvasId = `testu01-${test.key}`;
        canvas.id = canvasId;
        canvas.height = 140;

        body.appendChild(h5);
        body.appendChild(canvas);
        card.appendChild(body);
        col.appendChild(card);
        testu01Container.appendChild(col);

        const s = series(test.key);

        drawChart(
            canvasId,
            test.title,
            "p-value",
            s.mean,
            s.upper,
            s.lower,
            { line: "#8c564b", zone: "rgba(140,86,75,0.25)" },
            test.yMin,
            test.yMax
        );
    });
});
