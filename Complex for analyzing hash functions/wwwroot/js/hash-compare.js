document.addEventListener("DOMContentLoaded", function () {

    const ctx = document.getElementById("compareChart");
    if (!ctx) return;

    const meta = {
        sac: {
            title: "Сравнение алгоритмов по Avalanche Effect",
            yLabel: "Доля изменённых выходных битов"
        },
        bic: {
            title: "Сравнение алгоритмов по Bit Independence Criterion",
            yLabel: "Максимальная корреляция"
        },
        ham: {
            title: "Сравнение алгоритмов по расстоянию Хэмминга",
            yLabel: "Среднее расстояние Хэмминга (бит)"
        }
    };

    new Chart(ctx, {
        type: "bar",
        data: {
            labels: algorithms,
            datasets: [
                {
                    label: "Среднее значение",
                    data: mean,
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
                    text: meta[metric].title
                },
                tooltip: {
                    callbacks: {
                        label: ctx =>
                            `Среднее значение: ${ctx.parsed.y.toFixed(4)}`
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    title: {
                        display: true,
                        text: meta[metric].yLabel
                    }
                }
            }
        }
    });
});
