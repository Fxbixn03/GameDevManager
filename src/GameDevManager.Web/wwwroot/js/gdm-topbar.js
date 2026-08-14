// Misst, wie viele Modul-Knöpfe nebeneinander in die Leiste der Topbar passen. Wie bei den
// Karten kennt nur der Browser die dargestellte Breite; gemeldet wird deshalb allein die
// Geometrie — welche Module in den Aufklapp-Knopf wandern, entscheidet das MainLayout.
window.gdmTopbar = {
    observers: new WeakMap(),

    observe: function (element, dotNetRef) {
        if (!element) {
            return;
        }

        this.unobserve(element);

        // Nur Änderungen melden: Ein Neurendern der Leiste kostet sonst jedes Mal einen
        // Rundlauf, obwohl sich an der Breite nichts getan hat.
        let reported = null;
        const report = function () {
            const capacity = window.gdmTopbar.capacityOf(element);
            if (capacity === reported) {
                return;
            }

            reported = capacity;
            dotNetRef.invokeMethodAsync("SetCapacity", capacity).catch(function () {
                // Der Blazor-Kreis ist weg — dann wird hier auch nicht mehr gemessen.
                window.gdmTopbar.unobserve(element);
            });
        };

        const observer = new ResizeObserver(report);
        observer.observe(element);
        this.observers.set(element, observer);
    },

    // Wie viele Knöpfe der Breite des ersten hineinpassen; -1, wenn nichts zu messen ist.
    capacityOf: function (element) {
        const item = element.querySelector(".gdm-module-item");
        const width = item ? item.getBoundingClientRect().width : 0;

        // Ohne gerenderten Knopf gibt es keinen Maßstab. Dann lieber gar keine Vorgabe als
        // eine geratene — das MainLayout zeigt bei -1 alles.
        if (width <= 0) {
            return -1;
        }

        // Der halbe Pixel fängt die gebrochenen Breiten ab, mit denen ein genau passender
        // letzter Knopf sonst der Rundung zum Opfer fiele.
        return Math.max(0, Math.floor((element.clientWidth + 0.5) / width));
    },

    unobserve: function (element) {
        const observer = element ? this.observers.get(element) : null;
        if (observer) {
            observer.disconnect();
            this.observers.delete(element);
        }
    }
};
