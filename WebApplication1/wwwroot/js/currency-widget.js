document.addEventListener("DOMContentLoaded", function () {
    const baseSelect = document.getElementById("baseCurrency");
    const quoteSelect = document.getElementById("quoteCurrency");
    const loadBtn = document.getElementById("loadWidgetBtn");
    const resultDiv = document.getElementById("widgetResult");
    const selectedBase = document.getElementById("selectedBase");
    const selectedQuote = document.getElementById("selectedQuote");
    const currentRateSpan = document.getElementById("currentRate");
    const volatilitySpan = document.getElementById("volatility");
    const diffList = document.getElementById("diffList");
    const chartCanvas = document.getElementById("chart");

    let chart;

    loadBtn.addEventListener("click", async function () {
        const base = baseSelect.value;
        const quote = quoteSelect.value;

        if (!base || !quote) {
            alert("Vyberte prosím obě měny.");
            return;
        }

        try {
            const response = await fetch("/api/swop/widget", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ BaseCurrency: base, QuoteCurrency: quote })
            });

            if (!response.ok) {
                const text = await response.text();
                alert("Chyba při načítání dat: " + text);
                return;
            }

            const data = await response.json();

            selectedBase.textContent = data.Base;
            selectedQuote.textContent = data.Quote;
            currentRateSpan.textContent = data.CurrentRate.toFixed(4);
            volatilitySpan.textContent = data.Volatility.toFixed(4);

            // Vyplnit diff list
            diffList.innerHTML = "";
            data.PercentDiffs.forEach((pct, i) => {
                const li = document.createElement("li");
                li.textContent = `Den -${i + 1}: ${pct.toFixed(2)} %`;
                diffList.appendChild(li);
            });

            // Chart
            const labels = data.Historical.map(h => h.Timestamp.split('T')[0]);
            const rates = data.Historical.map(h => h.Rate);

            if (chart) chart.destroy();
            chart = new Chart(chartCanvas, {
                type: "line",
                data: { labels, datasets: [{ label: `${base} -> ${quote}`, data: rates, borderColor: "blue", fill: false }] },
                options: { responsive: true, plugins: { legend: { display: true } } }
            });

            resultDiv.classList.remove("d-none");
        } catch (err) {
            console.error(err);
            alert("Chyba při načítání dat z API.");
        }
    });
});
