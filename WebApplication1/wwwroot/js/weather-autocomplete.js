// wwwroot/js/weather-autocomplete.js
(function () {
    function initAll() {
        const forms = document.querySelectorAll('form[data-api]:not([data-autocomplete-initialized="1"])');
        forms.forEach((form) => initOne(form));
    }

    function initOne(form) {
        if (form.dataset.autocompleteInitialized === '1') return;
        form.dataset.autocompleteInitialized = '1';

        // DEBUG: uvidíme, který form se chytá
        console.log("[WX] initOne for form#", form.id || "(no-id)");

        // najdi input uvnitř daného formu
        const input =
            form.querySelector('#locationInput') ||                       // CurrentWeather
            form.querySelector('input[name="location"]') ||               // fallback
            form.querySelector('input[data-role="location-input"]') ||    // ForecastWeather
            form.querySelector('input[type="text"]');                     // úplnej fallback

        let suggestions =
            form.querySelector('#locationSuggestions') ||
            form.querySelector('.location-suggestions') ||
            form.querySelector('ul[id$="locationSuggestions"]');

        if (!suggestions) {
            // když UL chybí, vytvoříme ho
            suggestions = document.createElement('ul');
            suggestions.className = 'list-group position-absolute w-100 bg-white border rounded';
            suggestions.style.zIndex = '2000';
            suggestions.style.maxHeight = '250px';
            suggestions.style.overflowY = 'auto';
            suggestions.style.display = 'none';
            suggestions.id = 'auto_' + Math.random().toString(36).slice(2);
            form.appendChild(suggestions);
        }

        if (!input || !suggestions) {
            console.warn("[WX] autocomplete: nenašel jsem input/suggestions ve formu#", form.id);
            return;
        }

        // když máme hidden name="location", budeme ho plnit (Forecast pattern)
        const hidden = form.querySelector('input[type="hidden"][name="location"]');
        const apiUrl = form.dataset.api || '/api/locations';

        let items = [];
        let activeIndex = -1;
        let timer = null;

        const show = () => { suggestions.style.display = 'block'; };
        const hide = () => { suggestions.style.display = 'none'; activeIndex = -1; };
        const clear = () => { suggestions.innerHTML = ''; };

        const debounce = (fn, ms = 300) => (...args) => {
            clearTimeout(timer);
            timer = setTimeout(() => fn(...args), ms);
        };

        const setActive = (idx) => {
            const lis = suggestions.querySelectorAll('li');
            lis.forEach(li => li.classList.remove('active'));
            if (idx >= 0 && idx < lis.length) {
                lis[idx].classList.add('active');
                activeIndex = idx;
            } else {
                activeIndex = -1;
            }
        };

        const renderList = (arr) => {
            clear();
            if (!arr || arr.length === 0) {
                hide();
                return;
            }

            arr.forEach((it, idx) => {
                // Fallback: když endpoint nevrátí label/query, složíme si to sami
                const label = it.label || `${it.name}${it.region ? ', ' + it.region : ''}, ${it.country}`;
                const q = it.query || label || it.name;

                const li = document.createElement('li');
                li.className = 'list-group-item list-group-item-action';
                li.textContent = label;
                li.tabIndex = 0;

                li.addEventListener('mouseenter', () => setActive(idx));
                li.addEventListener('mouseleave', () => setActive(-1));
                li.addEventListener('click', () => {
                    input.value = q;
                    if (hidden) hidden.value = q;
                    hide();
                    form.requestSubmit();
                });

                suggestions.appendChild(li);
            });

            show();
            activeIndex = -1;
        };

        const fetchSuggestions = async (q) => {
            if (!q || q.trim().length < 2) {
                renderList([]);
                return;
            }
            try {
                const url = `${apiUrl}?q=${encodeURIComponent(q)}`;
                console.log("[WX] fetch:", url, "for form#", form.id || "(no-id)");
                const res = await fetch(url);
                if (!res.ok) {
                    console.warn("[WX] res not ok", res.status, "form#", form.id);
                    renderList([]);
                    return;
                }
                const data = await res.json();
                console.log("[WX] data:", data);
                items = Array.isArray(data) ? data : [];
                renderList(items);
            } catch (err) {
                console.error("[WX] fetch error:", err);
                renderList([]);
            }
        };

        input.addEventListener('input', debounce((e) => {
            fetchSuggestions(e.target.value);
        }, 300));

        input.addEventListener('keydown', (e) => {
            const lis = suggestions.querySelectorAll('li');
            if (e.key === 'ArrowDown') {
                e.preventDefault();
                if (lis.length === 0) return;
                setActive((activeIndex + 1) % lis.length);
            } else if (e.key === 'ArrowUp') {
                e.preventDefault();
                if (lis.length === 0) return;
                setActive((activeIndex - 1 + lis.length) % lis.length);
            } else if (e.key === 'Enter') {
                if (activeIndex >= 0 && activeIndex < items.length) {
                    e.preventDefault();
                    const it = items[activeIndex];
                    const label = it.label || `${it.name}${it.region ? ', ' + it.region : ''}, ${it.country}`;
                    const q = it.query || label || it.name;
                    input.value = q;
                    if (hidden) hidden.value = q;
                    hide();
                    form.requestSubmit();
                } else {
                    // Enter bez výběru – doplníme hidden, ať submit nese to, co je v inputu
                    if (hidden && hidden.value.trim() === '') hidden.value = input.value.trim();
                }
            } else if (e.key === 'Escape') {
                hide();
            }
        });

        // Submit bez výběru: pošli, co je napsané
        form.addEventListener('submit', () => {
            if (hidden && hidden.value.trim() === '') hidden.value = input.value.trim();
        });

        // klik mimo konkrétní widget schová jen jeho dropdown
        document.addEventListener('click', (e) => {
            if (!form.contains(e.target)) hide();
        });
    }

    document.addEventListener('DOMContentLoaded', initAll);
    const mo = new MutationObserver(initAll);
    mo.observe(document.documentElement, { childList: true, subtree: true });
})();
