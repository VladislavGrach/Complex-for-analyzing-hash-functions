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

    function drawChart(id, title, yLabel, mean, upper, lower, color, yMin = null, yMax = null) {
        const canvas = document.getElementById(id);
        if (!canvas) return;

        const chart = new Chart(canvas, {
            type: 'line',
            plugins: [whiteBackgroundPlugin],
            data: {
                labels: rounds,
                datasets: [
                    // Верхняя граница
                    {
                        data: upper,
                        borderWidth: 0,
                        pointRadius: 0,
                        tension: 0.35,
                        cubicInterpolationMode: 'monotone',
                        spanGaps: true
                    },
                    // Нижняя граница (заливка)
                    {
                        label: 'Доверительный интервал (±σ)',
                        data: lower,
                        fill: '-1',
                        backgroundColor: color.zone,
                        borderWidth: 0,
                        pointRadius: 0,
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
                        tension: 0.35,
                        cubicInterpolationMode: 'monotone',
                        fill: false,
                        spanGaps: true
                    }
                ]
            },
            options: {
                responsive: true,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            filter: function (item) {
                                // скрываем служебные dataset-ы
                                return item.text !== undefined;
                            }
                        }
                    },
                    title: {
                        display: true,
                        text: title
                    }
                },
                scales: {
                    x: {
                        title: {
                            display: true,
                            text: 'Число раундов'
                        }
                    },
                    y: {
                        min: yMin,
                        max: yMax,
                        title: {
                            display: true,
                            text: yLabel
                        }
                    }
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
        sac: {
            line: '#1f77b4',
            zone: 'rgba(31,119,180,0.25)'
        },
        bic: {
            line: '#d62728',
            zone: 'rgba(214,39,40,0.25)'
        },
        ham: {
            line: '#2ca02c',
            zone: 'rgba(44,160,44,0.25)'
        }
    };

    drawChart(
        "sacChart",
        "Strict Avalanche Criterion (SAC)",
        "Доля изменённых выходных битов",
        sacMean,
        sacUpper,
        sacLower,
        COLORS.sac,
        0,
        1
    );

    drawChart(
        "bicChart",
        "Bit Independence Criterion (BIC)",
        "Максимальная корреляция",
        bicMean,
        bicUpper,
        bicLower,
        COLORS.bic
    );

    drawChart(
        "hammingChart",
        "Среднее расстояние Хэмминга",
        "Расстояние Хэмминга (бит)",
        hamMean,
        hamUpper,
        hamLower,
        COLORS.ham
    );
});
