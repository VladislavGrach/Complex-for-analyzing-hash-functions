document.addEventListener("DOMContentLoaded", function () {

    // Плагин: всегда белый фон (для экрана и PNG)
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

    // Плагин: привязка tooltip к среднему (datasetIndex = 2), даже если оно скрыто.
    // Чтобы не мигало при клике по легенде — обрабатываем только mousemove.
    const snapTooltipToMeanPlugin = {
        id: 'snapTooltipToMean',
        afterEvent(chart, args) {
            const e = args.event;
            if (!e || !chart.tooltip) return;
            if (e.type !== 'mousemove') return; // [web:192]

            const meanVisible = chart.isDatasetVisible(2);
            const ciVisible = chart.isDatasetVisible(1);

            // Всё выключено => прячем tooltip полностью
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

            // Всегда якорим к "Среднее значение" (dataset 2)
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
                    // Верхняя граница (служебная)
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
                    // Нижняя граница / заливка (управляется легендой)
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
                    // Среднее
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

                            // включаем pointStyle для легенды (нужно для "Среднее значение")
                            usePointStyle: true,
                            pointStyleWidth: 12,

                            // "Доверительный интервал" не трогаем: вернём ему прямоугольник, а среднему дадим кружок цвета точки.
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
                                    }

                                    if (l.datasetIndex === 1) {
                                        // оставляем стандартный прямоугольник (как было)
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
                        displayColors: true, // цветные квадратики
                        callbacks: {
                            title: function (items) {
                                const chart = items[0]?.chart;
                                if (!chart) return null;

                                const meanVisible = chart.isDatasetVisible(2);
                                const ciVisible = chart.isDatasetVisible(1);

                                // Если всё выключено — вообще не показываем tooltip
                                if (!meanVisible && !ciVisible) return null;

                                return `Количество раундов: ${items[0].label}`;
                            },

                            // Контент формируем один раз (без дублей)
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

                                const fmt = (v) => Number(v).toFixed(3).replace('.', ',');

                                const lines = [];

                                // Среднее
                                if (meanVisible) {
                                    lines.push(`Среднее значение: ${fmt(meanVal)}`);
                                }

                                // Интервал
                                if (ciVisible) {
                                    const eps = 1e-12;
                                    const same = Math.abs(Number(upperVal) - Number(lowerVal)) < eps;

                                    // Дополнение: если верх==низ, показываем одну строку (иначе местами получалось "пусто")
                                    if (same) {
                                        lines.push(`Граница: ${fmt(upperVal)}`);
                                    } else {
                                        lines.push(`Верхняя граница: ${fmt(upperVal)}`);
                                        lines.push(`Нижняя граница: ${fmt(lowerVal)}`);
                                    }
                                }

                                return lines;
                            },

                            // Не добавляем строки на каждый tooltip item (чтобы не было дублей)
                            label: function () { return ''; },

                            // Цвет квадратика: если среднее включено — цвет точки среднего, иначе — цвет зоны интервала
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
                    y: { min: yMin, max: yMax, title: { display: true, text: yLabel } }
                }
            }
        });

        // ===== КНОПКА ЭКСПОРТА =====
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
        sac: { line: '#1f77b4', zone: 'rgba(31,119,180,0.25)' },
        bic: { line: '#d62728', zone: 'rgba(214,39,40,0.25)' },
        ham: { line: '#2ca02c', zone: 'rgba(44,160,44,0.25)' }
    };

    drawChart("sacChart", "Strict Avalanche Criterion (SAC)", "Доля изменённых выходных битов",
        sacMean, sacUpper, sacLower, COLORS.sac, 0, 1);

    drawChart("bicChart", "Bit Independence Criterion (BIC)", "Максимальная корреляция",
        bicMean, bicUpper, bicLower, COLORS.bic);

    drawChart("hammingChart", "Среднее расстояние Хэмминга", "Расстояние Хэмминга (бит)",
        hamMean, hamUpper, hamLower, COLORS.ham);
});
