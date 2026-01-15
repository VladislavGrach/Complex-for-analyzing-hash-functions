document.addEventListener("DOMContentLoaded", function () {

    const ctx = document.getElementById("compareChart");
    if (!ctx) return;

    let chart = null;

    function build(metric) {

        let mean, yLabel, title;

        if (metric === "sac") {
            mean = sacMean;
            yLabel = "Доля изменённых выходных битов";
            title = "Сравнение алгоритмов по Avalanche Effect";
        }
        else if (metric === "bic") {
            mean = bicMean;
            yLabel = "Максимальная корреляция";
            title = "Сравнение алгоритмов по Bit Independence Criterion";
        }
        else {
            mean = hamMean;
            yLabel = "Среднее расстояние Хэмминга (бит)";
            title = "Сравнение алгоритмов по расстоянию Хэмминга";
        }

        if (chart) {
            chart.destroy();
        }

        chart = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: algorithms,
                datasets: [
                    {
                        label: 'Среднее значение',
                        data: mean,
                        backgroundColor: '#1f77b4',
                        borderColor: '#1f77b4',
                        borderWidth: 1
                    }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: {
                        position: 'bottom'
                    },
                    title: {
                        display: true,
                        text: title
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        title: {
                            display: true,
                            text: yLabel
                        }
                    }
                }
            }
        });
    }

    build("sac");

    // Переключатель метрик
    document.querySelectorAll("input[name=metric]")
        .forEach(radio =>
            radio.addEventListener("change", e => build(e.target.value))
        );
});
