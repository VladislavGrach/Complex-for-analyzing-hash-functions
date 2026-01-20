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

    // ===== ПОДГОТОВКА ДАННЫХ =====
    function buildExportRows() {
        return (algorithms || []).map((alg, i) => ({
            Algorithm: alg,
            Metric: key.toUpperCase(),
            Mean: mean[i]
        }));
    }

    // ===== CSV =====
    function exportCSV() {
        const rows = buildExportRows();
        if (!rows.length) return;

        const header = Object.keys(rows[0]).join(",");
        const body = rows
            .map(r => Object.values(r).join(","))
            .join("\n");

        downloadFile(
            header + "\n" + body,
            `comparison_${key}.csv`,
            "text/csv;charset=utf-8;"
        );
    }

    // ===== JSON =====
    function exportJSON() {
        const rows = buildExportRows();

        downloadFile(
            JSON.stringify(rows, null, 2),
            `comparison_${key}.json`,
            "application/json"
        );
    }

    function downloadFile(content, filename, mime) {
        const blob = new Blob([content], { type: mime });
        const url = URL.createObjectURL(blob);

        const a = document.createElement("a");
        a.href = url;
        a.download = filename;
        a.click();

        URL.revokeObjectURL(url);
    }

    // ===== ОБРАБОТЧИКИ КНОПОК =====
    if (exportCsvBtn) {
        exportCsvBtn.addEventListener("click", exportCSV);
    }

    if (exportJsonBtn) {
        exportJsonBtn.addEventListener("click", exportJSON);
    }
});
