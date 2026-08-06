/*
 * Rechnet einen Mausklick in eine Lage relativ zum Kartenbild um (0 bis 1).
 *
 * Nötig, weil die Serverseite die dargestellte Bildgröße nicht kennt: Blazor liefert im
 * MouseEventArgs nur Bildschirmkoordinaten, und das Bild skaliert mit dem Fenster. Die
 * Markierungen werden deshalb relativ gespeichert — so bleiben sie richtig, egal wie groß
 * die Karte gerade dargestellt wird.
 */
window.gdmMap = {
    relativePoint: function (element, clientX, clientY) {
        if (!element) {
            return null;
        }

        const rect = element.getBoundingClientRect();
        if (rect.width <= 0 || rect.height <= 0) {
            return null;
        }

        const clamp = value => Math.min(1, Math.max(0, value));

        return {
            x: clamp((clientX - rect.left) / rect.width),
            y: clamp((clientY - rect.top) / rect.height)
        };
    }
};
