// Misst die Modulleiste der Topbar: wie breit sie ist und wie breit ein Modul-Knopf. Wie bei
// den Karten kennt nur der Browser die dargestellte Größe; gemeldet wird allein die Geometrie —
// wie viele Module das ergibt und welche in den Aufklapp-Knopf wandern, entscheidet das
// MainLayout.
window.gdmTopbar = {
    entries: new WeakMap(),

    observe: function (element, dotNetRef) {
        if (!element) {
            return;
        }

        this.unobserve(element);

        // Nur Änderungen melden: Ein Neurendern der Leiste kostet sonst jedes Mal einen
        // Rundlauf, obwohl sich an den Maßen nichts getan hat.
        let reported = "";
        const report = function () {
            const size = window.gdmTopbar.measure(element);
            const key = size.available.toFixed(1) + "/" + size.unit.toFixed(1);
            if (key === reported) {
                return;
            }

            reported = key;
            dotNetRef.invokeMethodAsync("SetMetrics", size.available, size.unit).catch(function () {
                // Der Blazor-Kreis ist weg — dann wird hier auch nicht mehr gemessen.
                window.gdmTopbar.unobserve(element);
            });
        };

        const observer = new ResizeObserver(report);
        observer.observe(element);

        // Zusätzlich zum Beobachter: Beim Zoomen ändert sich die Breite der Leiste in
        // CSS-Pixeln nicht zwingend — die Knöpfe werden mitskaliert, das Fenster meldet den
        // Wechsel aber. Ohne das bliebe eine veraltete Aufteilung stehen.
        window.addEventListener("resize", report);
        if (window.visualViewport) {
            window.visualViewport.addEventListener("resize", report);
        }

        this.entries.set(element, { observer: observer, report: report });
    },

    // available: Platz für die Modul-Knöpfe, unit: Breite eines Knopfes. Gemittelt über die
    // gerenderten Knöpfe statt am ersten abgelesen — auf gebrochenen Zoomstufen rastet jede
    // Kante einzeln auf ganze Gerätepixel ein, und ein einzelner Knopf misst dann bis zu einem
    // Pixel daneben. Über zwanzig Knöpfe summiert sich das zu einem ganzen Symbol.
    measure: function (element) {
        const items = element.querySelectorAll(".gdm-module-item");
        const available = element.getBoundingClientRect().width;

        if (!items.length) {
            return { available: available, unit: 0 };
        }

        const first = items[0].getBoundingClientRect();
        const last = items[items.length - 1].getBoundingClientRect();
        const unit = items.length > 1
            ? (last.right - first.left) / items.length
            : first.width;

        return { available: available, unit: unit };
    },

    unobserve: function (element) {
        const entry = element ? this.entries.get(element) : null;
        if (!entry) {
            return;
        }

        entry.observer.disconnect();
        window.removeEventListener("resize", entry.report);
        if (window.visualViewport) {
            window.visualViewport.removeEventListener("resize", entry.report);
        }

        this.entries.delete(element);
    }
};
