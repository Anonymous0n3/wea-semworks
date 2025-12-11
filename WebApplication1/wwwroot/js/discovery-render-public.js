// discovery-render-public.js

export function renderCard(w) {
    // Escapování dat
    const widgetDataStr = JSON.stringify(w.widgetData).replace(/"/g, '&quot;');
    const likesCount = w.likesCount || 0;

    return `
    <div class="col-md-4 col-lg-3">
        <div class="card h-100 shadow-sm">
            <div class="card-body d-flex flex-column">
                <h5 class="card-title text-truncate" title="${w.publicName}">${w.publicName}</h5>
                <div class="mb-2">
                    <span class="badge bg-info text-dark">${w.widgetType}</span>
                </div>
                <p class="card-text small text-muted mb-1">${window.translations.autor}: ${w.authorName}</p>
                <p class="card-text small text-muted mb-auto">${window.translations.locale}: ${w.widgetData.location || "N/A"}</p>
                
                <button class="btn btn-primary btn-sm w-100 mt-3" onclick="window.previewWidget('${w.widgetType}', ${widgetDataStr})">
                    👁️ ${window.translations.preview}
                </button>
            </div>
            <div class="card-footer bg-white border-top-0 d-flex justify-content-between align-items-center py-2">
                <span class="text-muted fw-bold">❤️ ${likesCount}</span>
                <small class="text-muted">${new Date(w.createdAt).toLocaleDateString()}</small>
            </div>
        </div>
    </div>
    `;
}

export function renderEmptyState() {
    return '<div class="col-12 text-center text-muted py-5">@Localizer["Pro zobrazení oblíbených položek se musíte přihlásit"].</div>';
}