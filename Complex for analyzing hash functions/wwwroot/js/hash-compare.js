document.addEventListener("DOMContentLoaded", function () {

    const canvas = document.getElementById("compareChart");
    if (!canvas) return;

    const meta = {
        sac: {
            title: "Сравнение алгоритмов по Avalanche Effect",
            yLabel: "Доля изменённых выходных битов"
        },
        bic: {
            title: "Сравнение алгоритмов по Bit Independence Criterion",
            yLabel: "Максимальная корреляция"
        },
        mono: {
            title: "Сравнение алгоритмов по Monobit test (NIST)",
            yLabel: "p-value"
        }
    };

    const key = (metric || "sac").toString().trim().toLowerCase();
    const cfg = meta[key] || meta.sac;

    new Chart(canvas, {
        type: "bar",
        data: {
            labels: algorithms || [],
            datasets: [
                {
                    label: "Среднее значение",
                    data: mean || [],
                    backgroundColor: "#1f77b4",
                    borderColor: "#1f77b4",
                    borderWidth: 1
                }
            ]
        },
        options: {
            responsive: true,
            plugins: {
                legend: { position: "bottom" },
                title: {
                    display: true,
                    text: cfg.title
                },
                tooltip: {
                    callbacks: {
                        label: (ctx) => `Среднее значение: ${Number(ctx.parsed.y).toFixed(4)}`
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: cfg.yLabel
                    }
                }
            }
        }
    });
});
