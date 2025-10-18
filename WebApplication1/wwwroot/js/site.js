// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {
    const langSelect = document.getElementById("langSelect");

    // Když na stránce není přepínač jazyka, nic nedělej
    if (!langSelect) {
        return;
    }

    const LS_KEY = "preferredLang";
    const storedLang = localStorage.getItem(LS_KEY);

    // preferuj uloženou volbu; jinak vezmi z prohlížeče (en/cs)
    const initialLang = storedLang
        ? storedLang
        : (navigator.language && navigator.language.toLowerCase().startsWith("en") ? "en" : "cs");

    // nastav select (existuje → safe)
    langSelect.value = initialLang;

    // ulož do localStorage, pokud tam ještě nebylo
    if (!storedLang) {
        localStorage.setItem(LS_KEY, initialLang);
    }

    // změna jazyka → ulož + přesměruj
    langSelect.addEventListener("change", function () {
        const val = this.value || "cs";
        localStorage.setItem(LS_KEY, val);
        // Pokud používáš route typu /set-language (POST) s cookie, tenhle redirect klidně smaž.
        // Tady nechávám jednoduchou variantu s prefixem v URL:
        window.location.href = `/${val}`;
    });
});
