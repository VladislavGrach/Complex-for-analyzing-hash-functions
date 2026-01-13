document.addEventListener("DOMContentLoaded", function () {

    // ---------- Avalanche ----------
    const sacCtx = document.getElementById("sacChart");
    if (sacCtx) {
        new Chart(sacCtx, {
            type: 'line',
            data: {
                labels: rounds,
                datasets: [
                    {
                        label: 'Mean Flip Rate',
                        data: meanFlipRates,
                        borderWidth: 2,
                        tension: 0.25
                    },
                    {
                        label: 'StdDev Flip Rate',
                        data: stdFlipRates,
                        borderDash: [5, 5],
                        borderWidth: 2,
                        tension: 0.25
                    }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    title: {
                        display: true,
                        text: 'Strict Avalanche Criterion (SAC)'
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
                        min: 0,
                        max: 1,
                        title: {
                            display: true,
                            text: 'Доля изменённых выходных битов'
                        }
                    }
                }
            }

        });
    }

    // ---------- BIC ----------
    const bicCtx = document.getElementById("bicChart");
    if (bicCtx) {
        new Chart(bicCtx, {
            type: 'line',
            data: {
                labels: rounds,
                datasets: [
                    {
                        label: 'Max |Correlation|',
                        data: bicMax,
                        borderWidth: 2,
                        tension: 0.25
                    },
                    {
                        label: 'Std Correlation',
                        data: bicStd,
                        borderDash: [5, 5],
                        borderWidth: 2,
                        tension: 0.25
                    }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    title: {
                        display: true,
                        text: 'Bit Independence Criterion (BIC)'
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
                        title: {
                            display: true,
                            text: 'Абсолютное значение корреляции'
                        }
                    }
                }
            }

        });
    }

    // ---------- Hamming ----------
    const hamCtx = document.getElementById("hammingChart");
    if (hamCtx) {
        new Chart(hamCtx, {
            type: 'line',
            data: {
                labels: rounds,
                datasets: [
                    {
                        label: 'Average Hamming Distance',
                        data: avgHamming,
                        borderWidth: 2,
                        tension: 0.25
                    }
                ]
            },
            options: {
                responsive: true,
                plugins: {
                    title: {
                        display: true,
                        text: 'Среднее расстояние Хэмминга'
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
                        title: {
                            display: true,
                            text: 'Среднее расстояние Хэмминга (бит)'
                        }
                    }
                }
            }

        });
    }
});
