// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {
    const langSelect = document.getElementById("langSelect");

    // 1. Ochrana: Pokud element neexistuje, skonči
    if (!langSelect) {
        return;
    }

    const LS_KEY = "preferredLang";
    // Zkusíme načíst z localStorage, jinak default (zde natvrdo 'cs', nebo logika navigatoru)
    const storedLang = localStorage.getItem(LS_KEY);

    // Nastavení počáteční hodnoty selectu
    // POZNÁMKA: Ideální je, aby 'value' nastavil už server v Razor pohledu (atribut selected),
    // ale toto je funkční pojistka na straně klienta.
    if (storedLang) {
        langSelect.value = storedLang;
    }

    // 2. Event Listener pro změnu
    langSelect.addEventListener("change", function () {
        const val = this.value; // 'cs' nebo 'en'

        // Uložíme volbu pro příště
        localStorage.setItem(LS_KEY, val);

        // 3. DŮLEŽITÉ: Musíme říct serveru, aby nastavil Cookie!
        // Bez tohoto kroku server neví, že došlo ke změně.
        const returnUrl = encodeURIComponent(window.location.pathname + window.location.search);

        fetch('/set-language', {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
            body: `culture=${val}&returnUrl=${returnUrl}`
        }).then(() => {
            // 4. Obnovení stránky
            // Používáme přiřazení href samo sobě, aby se obešla cache prohlížeče
            // a stránka se načetla znovu se správným jazykem ze serveru.
            window.location.href = window.location.href;
        }).catch(err => {
            console.error("Chyba při změně jazyka:", err);
        });
    });
});
