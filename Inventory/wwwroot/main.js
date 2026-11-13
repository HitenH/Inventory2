
window.setFocus = (element) => {
    element.focus();
};

// wwwroot/js/fileDownload.js
window.downloadFile = (options) => {
    const blob = new Blob([new Uint8Array(options.byteArray)], { type: options.contentType });
    const link = document.createElement('a');
    link.href = window.URL.createObjectURL(blob);
    link.download = options.fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};

// Batch 17: colour and light/dark choice, remembered on this computer.
window.inventoryTheme = {
    load() {
        try { return JSON.parse(localStorage.getItem('inventory-theme')); } catch { return null; }
    },
    save(colour, mode) {
        try { localStorage.setItem('inventory-theme', JSON.stringify({ colour, mode })); } catch { }
    }
};
