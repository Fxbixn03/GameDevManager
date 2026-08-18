// Die Wellenform einer Audio-Datei. Der Browser weiß, wie man Audio dekodiert — gerechnet
// und gezeichnet wird deshalb hier, clientseitig; der Server rendert nichts. Dieselbe
// Trennung wie bei den Karten: Was nur der Browser weiß, bleibt im Browser.
window.gdmAudio = {
    draw: async function (canvas, url) {
        if (!canvas || !window.AudioContext) {
            return;
        }

        try {
            const response = await fetch(url);
            if (!response.ok) {
                return;
            }

            const context = new AudioContext();
            let audio;
            try {
                audio = await context.decodeAudioData(await response.arrayBuffer());
            } finally {
                context.close();
            }

            const channel = audio.getChannelData(0);
            const width = canvas.width = canvas.clientWidth || 300;
            const height = canvas.height = canvas.clientHeight || 40;
            const step = Math.max(1, Math.floor(channel.length / width));
            const ctx = canvas.getContext("2d");

            ctx.clearRect(0, 0, width, height);
            // Die Farbe kommt aus dem CSS (currentColor) — so folgt die Welle dem Theme.
            ctx.fillStyle = getComputedStyle(canvas).color;

            for (let x = 0; x < width; x++) {
                let peak = 0;
                const start = x * step;

                // Abgetastet wird jede 16. Probe des Fensters: Für eine 40 Pixel hohe
                // Dekoration reicht das, und eine Minute Audio bleibt flüssig.
                for (let i = start; i < start + step && i < channel.length; i += 16) {
                    const value = Math.abs(channel[i]);
                    if (value > peak) {
                        peak = value;
                    }
                }

                const bar = Math.max(1, peak * height);
                ctx.fillRect(x, (height - bar) / 2, 1, bar);
            }
        } catch {
            // Die Wellenform ist Dekoration — ohne sie spielt der Player trotzdem.
        }
    }
};
