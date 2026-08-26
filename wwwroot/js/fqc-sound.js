// ══════════════════════════════════════════════════════════
// FQC sound — phát tiếng "PASS" / "NG" khi quét
// Dùng chung cho toàn bộ Views/FQC/*.cshtml
// Không cần file audio: âm thanh được tạo bằng Web Audio API.
// ══════════════════════════════════════════════════════════
(function (global) {
    'use strict';

    const STORAGE_KEY = 'fqcSoundEnabled';
    let ctx = null;

    function getCtx() {
        if (!ctx) {
            const AudioCtx = window.AudioContext || window['webkitAudioContext'];
            if (!AudioCtx) return null;
            ctx = new AudioCtx();
        }
        if (ctx.state === 'suspended') ctx.resume();
        return ctx;
    }

    function isEnabled() {
        return localStorage.getItem(STORAGE_KEY) !== '0';
    }

    function setEnabled(v) {
        localStorage.setItem(STORAGE_KEY, v ? '1' : '0');
    }

    // 1 tiếng "ting" trong trẻo kiểu chuông: tần số gốc + họa âm cao hơn, tắt dần tự nhiên
    function bell(freq, duration, startAt, volume) {
        const audioCtx = getCtx();
        if (!audioCtx) return;
        const t0 = audioCtx.currentTime + (startAt || 0);
        const t1 = t0 + duration;
        const vol = volume ?? 1;

        [{ f: freq, g: 1 }, { f: freq * 2.4, g: 0.35 }].forEach(({ f, g }) => {
            const osc  = audioCtx.createOscillator();
            const gain = audioCtx.createGain();
            osc.type = 'sine';
            osc.frequency.value = f;
            gain.gain.setValueAtTime(0, t0);
            gain.gain.linearRampToValueAtTime(vol * g, t0 + 0.008);
            gain.gain.exponentialRampToValueAtTime(0.0001, t1);
            osc.connect(gain);
            gain.connect(audioCtx.destination);
            osc.start(t0);
            osc.stop(t1 + 0.02);
        });
    }

    // Tiếng "tèo" — kiểu kèn buồn (sad trombone), tụt dần xuống thấp, kéo dài hơn
    function womp(startAt) {
        const audioCtx = getCtx();
        if (!audioCtx) return;
        const t0 = audioCtx.currentTime + (startAt || 0);
        const duration = 1.3;
        const t1 = t0 + duration;

        const osc  = audioCtx.createOscillator();
        const gain = audioCtx.createGain();
        osc.type = 'sawtooth';

        // Trượt cao độ xuống theo từng bậc, y hệt tiếng kèn "tèo... tèo... teo..."
        osc.frequency.setValueAtTime(330, t0);
        osc.frequency.linearRampToValueAtTime(294, t0 + 0.38);
        osc.frequency.linearRampToValueAtTime(220, t0 + 0.8);
        osc.frequency.linearRampToValueAtTime(130, t1);

        gain.gain.setValueAtTime(0, t0);
        gain.gain.linearRampToValueAtTime(1, t0 + 0.03);
        gain.gain.setValueAtTime(1, t0 + duration * 0.75);
        gain.gain.exponentialRampToValueAtTime(0.0001, t1);

        // Lọc bớt tần số cao để nghe "ù ù" như kèn, đỡ chói
        const filter = audioCtx.createBiquadFilter();
        filter.type = 'lowpass';
        filter.frequency.value = 900;

        osc.connect(filter);
        filter.connect(gain);
        gain.connect(audioCtx.destination);
        osc.start(t0);
        osc.stop(t1 + 0.02);
    }

    function playPass() {
        if (!isEnabled()) return;
        // 2 tiếng "ting ting" ngân dài — báo hiệu đạt
        bell(1568, 0.7, 0);
        bell(1760, 0.9, 0.22);
    }

    function playNg() {
        if (!isEnabled()) return;
        // Tiếng "tèo" kiểu kèn buồn, kéo dài — báo hiệu lỗi
        womp(0);
    }

    global.fqcSound = { playPass, playNg, isEnabled, setEnabled };

})(window);
