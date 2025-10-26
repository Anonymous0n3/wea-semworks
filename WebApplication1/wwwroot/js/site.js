// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {
    const langSelect = document.getElementById("langSelect");

    // Když tu prvek vůbec není (třeba na dashboardu), prostě skončíme
    if (!langSelect) return;

    // Bezpečné čtení/zápis do localStorage (když je zakázaný, ať to nespadne)
    const getLS = (k) => {
        try { return window.localStorage.getItem(k); } catch { return null; }
    };
    const setLS = (k, v) => {
        try { window.localStorage.setItem(k, v); } catch { /* ignore */ }
    };

    const storedLang = getLS("preferredLang");

    if (storedLang) {
        langSelect.value = storedLang;
    } else {
        const navLang = (navigator.language || navigator.userLanguage || "cs").toLowerCase();
        const browserLang = navLang.startsWith("en") ? "en" : "cs";
        langSelect.value = browserLang;
        setLS("preferredLang", browserLang);
    }

    langSelect.addEventListener("change", function () {
        const val = this.value || "cs";
        setLS("preferredLang", val);
        // přesměrování na /cs nebo /en
        window.location.href = `/${val}`;
    });
});
