// Sinal de chamada sintetizado com Web Audio: não depende de arquivo de áudio nem de licença de terceiros.
// Comum: "ding-dong" descendente (mi-dó). Preferencial: três notas ascendentes (dó-mi-sol), reconhecível sem olhar o painel.
const COMMON_CHIME = [{ frequency: 659.25, at: 0 }, { frequency: 523.25, at: 0.55 }];
const PREFERENTIAL_CHIME = [{ frequency: 523.25, at: 0 }, { frequency: 659.25, at: 0.3 }, { frequency: 783.99, at: 0.6 }];

// Parciais de sino: fundamental, oitava, quinta da oitava e um parcial inarmônico que dá o brilho metálico
const PARTIALS = [{ ratio: 1, level: 1 }, { ratio: 2, level: 0.35 }, { ratio: 3, level: 0.18 }, { ratio: 4.2, level: 0.06 }];
const NOTE_LEVEL = 0.32;
const ATTACK = 0.008;
const DECAY = 1.6;
const MASTER_LEVEL = 1.1;

let audio = null;
let output = null;

// Navegadores só liberam áudio depois de uma interação do usuário; o botão "Ativar som" chama esta função
export async function enableSound() {
    if (!audio) {
        audio = new (window.AudioContext || window.webkitAudioContext)();
        const master = audio.createGain();
        master.gain.value = MASTER_LEVEL;
        // O compressor evita estalos quando uma nova chamada começa antes de o sino anterior terminar
        const compressor = audio.createDynamicsCompressor();
        master.connect(compressor);
        compressor.connect(audio.destination);
        output = master;
    }

    await audio.resume();
    return audio.state === 'running';
}

export function playCall(preferential = false) {
    if (!audio || audio.state !== 'running') {
        return;
    }

    scheduleChime(audio, output, audio.currentTime, preferential);
}

export async function fullscreen() {
    if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
    } else {
        await document.exitFullscreen();
    }
}

// Agenda as notas do sinal a partir de "start" e devolve a duração total em segundos
export function scheduleChime(context, destination, start, preferential) {
    const notes = preferential ? PREFERENTIAL_CHIME : COMMON_CHIME;
    for (const note of notes) {
        scheduleBell(context, destination, note.frequency, start + note.at);
    }

    return notes[notes.length - 1].at + DECAY;
}

// Os parciais agudos morrem antes da fundamental, como no bronze de um sino real
function scheduleBell(context, destination, frequency, at) {
    for (const partial of PARTIALS) {
        const oscillator = context.createOscillator();
        const volume = context.createGain();
        const end = at + DECAY / Math.sqrt(partial.ratio);
        oscillator.type = 'sine';
        oscillator.frequency.value = frequency * partial.ratio;
        volume.gain.setValueAtTime(0, at);
        volume.gain.linearRampToValueAtTime(NOTE_LEVEL * partial.level, at + ATTACK);
        volume.gain.exponentialRampToValueAtTime(0.0001, end);
        oscillator.connect(volume);
        volume.connect(destination);
        oscillator.start(at);
        oscillator.stop(end + 0.05);
    }
}
