// Globale Tastatur-Shortcuts. Der Listener wird nur einmal registriert; die .NET-Referenz
// wird bei jedem Verbindungsaufbau erneuert, weil ein neuer Blazor-Kreis eine neue bekommt.
//
// Die Aufteilung folgt der Linie des Hauses: Der Browser meldet, was nur er weiß (welche Taste,
// wo steht der Fokus, gibt es hier einen markierten Knopf), entschieden wird in C#. Alles, was
// eine Adresse kennt oder einen Dialog öffnet, ruft deshalb zurück.
window.gdmShortcuts = {
    ref: null,

    // Welche Taste welche Adresse öffnet, steht in C# (KeyboardShortcuts.Routes) — hier stünde
    // sie ein zweites Mal und liefe beim ersten neuen Modul auseinander.
    routes: {},

    init: function (dotNetRef, routes) {
        this.ref = dotNetRef;
        this.routes = routes || {};

        if (this._bound) {
            return;
        }
        this._bound = true;

        document.addEventListener("keydown", function (e) {
            const self = window.gdmShortcuts;
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

            // Strg+P: die Kommandopalette. Der Browser-Druckdialog liegt auf derselben
            // Taste — hier wird er bewusst verdrängt, weil eine Seite dieser Anwendung
            // gedruckt selten Sinn ergibt und die Palette der häufigere Wunsch ist.
            if ((e.ctrlKey || e.metaKey) && !e.shiftKey && !e.altKey && e.key.toLowerCase() === "p"
                && window.gdmShortcuts.ref) {
                e.preventDefault();
                window.gdmShortcuts.ref.invokeMethodAsync("ShowPalette");
                return;
            }

            if (e.ctrlKey || e.metaKey) {
                return;
            }

            // Alt+Buchstabe: Navigation — nicht beim Tippen, dort gehören die Tasten dem Feld.
            if (e.altKey && !typing && self.ref) {
                const route = self.routes[e.key.toLowerCase()];
                if (route) {
                    e.preventDefault();
                    self.ref.invokeMethodAsync("NavigateTo", route);
                }
                return;
            }

            if (e.altKey || typing) {
                return;
            }

            // Ab hier die Tasten ohne Zusatztaste. Sie gelten nur außerhalb von Eingabefeldern —
            // sonst ließe sich kein „n“ mehr tippen.

            // „?“ zeigt die Übersicht. Auf einer deutschen Tastatur liegt es auf Shift+ß;
            // gefragt wird deshalb nach dem erzeugten Zeichen und nicht nach der Taste.
            if (e.key === "?" && self.ref) {
                e.preventDefault();
                self.ref.invokeMethodAsync("ShowOverview");
                return;
            }

            // „n“ legt neu an: der Knopf, den die Liste mit data-gdm-new markiert hat.
            if (e.key.toLowerCase() === "n") {
                const create = document.querySelector("[data-gdm-new]");
                if (create) {
                    e.preventDefault();
                    create.click();
                }
                return;
            }

            // „e“ öffnet einen Eintrag der Liste — dieselbe Kachel, die ein Klick öffnete.
            // Kein eigener Zustand: Was „markiert“ ist, weiß der Browser über den Fokus, und
            // ohne Fokus ist der erste Eintrag die naheliegende Wahl.
            //
            // Gesucht wird über die Kachel-Klasse und nicht über eine eigene Marke: Sie steht
            // in jeder Modul-Liste ohnehin, und eine Marke müsste in zwanzig Seiten gepflegt
            // werden, damit ein neues Modul nicht stillschweigend fehlt.
            if (e.key.toLowerCase() === "e") {
                const selector = ".gdm-card-link a[href], .gdm-item-grid a[href]";
                const focused = document.activeElement;
                const inCard = focused && focused.closest && focused.closest(".gdm-card-link");
                const link = (inCard && inCard.querySelector("a[href]")) || document.querySelector(selector);

                if (link) {
                    e.preventDefault();
                    link.click();
                }
            }
        });
    }
};
