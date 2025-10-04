// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

document.addEventListener("DOMContentLoaded", function () {
    const langSelect = document.getElementById("langSelect");
    const storedLang = localStorage.getItem("preferredLang");

    if (storedLang) {
        langSelect.value = storedLang;
    } else {
        const browserLang = navigator.language.startsWith("en") ? "en" : "cs";
        langSelect.value = browserLang;
        localStorage.setItem("preferredLang", browserLang);
    }

    langSelect.addEventListener("change", function () {
        localStorage.setItem("preferredLang", this.value);
        window.location.href = `/${this.value}`;
    });
});


