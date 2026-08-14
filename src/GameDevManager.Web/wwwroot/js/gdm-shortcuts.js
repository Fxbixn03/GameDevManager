// Globale Tastatur-Shortcuts. Der Listener wird nur einmal registriert; die .NET-Referenz
// wird bei jedem Verbindungsaufbau erneuert, weil ein neuer Blazor-Kreis eine neue bekommt.
window.gdmShortcuts = {
    ref: null,

    init: function (dotNetRef) {
        this.ref = dotNetRef;

        if (this._bound) {
            return;
        }
        this._bound = true;

        document.addEventListener("keydown", function (e) {
            const target = e.target || {};
            const tag = (target.tagName || "").toLowerCase();
            const typing = tag === "input" || tag === "textarea" || target.isContentEditable === true;

            // Strg+K: globale Suche fokussieren — auch aus einem Eingabefeld heraus.
            if ((e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey && e.key.toLowerCase() === "k") {
                const input = document.querySelector("#gdm-global-search input");
                if (input) {
                    e.preventDefault();
                    input.focus();
                }
                return;
            }

            // Strg+S: Speichern der geöffneten Maske, wenn sie einen Speichern-Knopf markiert hat.
            if ((e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey && e.key.toLowerCase() === "s") {
                const save = document.querySelector("[data-gdm-save]");
                if (save) {
                    e.preventDefault();
                    save.click();
                }
                return;
            }

            // Alt+Buchstabe: Navigation — nicht beim Tippen, dort gehören die Tasten dem Feld.
            if (e.altKey && !e.ctrlKey && !e.metaKey && !typing && window.gdmShortcuts.ref) {
                const routes = {
                    "h": "/",
                    "i": "/modules/items",
                    "n": "/modules/npcs",
                    "q": "/modules/quests",
                    "t": "/modules/todo",
                    "w": "/modules/whiteboard"
                };
                const route = routes[e.key.toLowerCase()];
                if (route) {
                    e.preventDefault();
                    window.gdmShortcuts.ref.invokeMethodAsync("NavigateTo", route);
                }
            }
        });
    }
};
