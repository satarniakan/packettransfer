/* packettransfer.App/wwwroot/barcode-scan.js — اسکن بارکد تحویل با دوربین گوشی.
   از BarcodeDetector داخلی مرورگر (کروم/اج اندروید) استفاده می‌کند؛
   اگر پشتیبانی نشود، ورود دستی در صفحه همیشه هست. */
let activeStream = null;
let activeTimer = null;

export function isSupported() {
    return ('BarcodeDetector' in window) && !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
}

export async function startScanner(video, dotNetRef, field) {
    stopScanner();

    const stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment' },
        audio: false
    });
    activeStream = stream;
    video.srcObject = stream;
    await video.play();

    const formats = ['qr_code', 'code_128', 'code_39', 'ean_13', 'ean_8', 'itf', 'codabar', 'data_matrix'];
    let detector;
    try {
        detector = new BarcodeDetector({ formats });
    } catch {
        detector = new BarcodeDetector();
    }

    activeTimer = setInterval(async () => {
        if (!video || video.readyState < 2 || video.videoWidth === 0) return;
        try {
            const codes = await detector.detect(video);
            if (codes && codes.length > 0 && codes[0].rawValue) {
                const value = codes[0].rawValue;
                stopScanner();
                await dotNetRef.invokeMethodAsync('OnBarcodeScanned', field, value);
            }
        } catch {
            // فریم خراب — فریم بعدی
        }
    }, 400);
}

export function stopScanner() {
    if (activeTimer) {
        clearInterval(activeTimer);
        activeTimer = null;
    }
    if (activeStream) {
        activeStream.getTracks().forEach(t => t.stop());
        activeStream = null;
    }
}
