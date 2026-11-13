
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

// Batch 17 round 4: floating labels. Boxes with a small label (label.form-label) get an invisible placeholder (" "), so the
// CSS (tables.css, round 4) can see when a box is empty and show the label inside it. Boxes with a real placeholder are left alone.
(function () {
    function mark(root) {
        if (!root || !root.querySelectorAll) return;
        var labels = root.matches && root.matches('label.form-label') ? [root] : [];
        labels = labels.concat(Array.prototype.slice.call(root.querySelectorAll('label.form-label')));
        if (root.closest) { var up = root.closest('div'); if (up) labels = labels.concat(Array.prototype.slice.call(up.querySelectorAll(':scope > label.form-label'))); }
        labels.forEach(function (l) {
            var host = l.parentElement; if (!host) return;
            host.querySelectorAll('input:not([placeholder]):not([type=checkbox]):not([type=radio]):not([type=file]):not([type=hidden]), textarea:not([placeholder])')
                .forEach(function (i) { i.setAttribute('placeholder', ' '); });
        });
    }
    function start() {
        mark(document);
        new MutationObserver(function (ms) {
            ms.forEach(function (m) { m.addedNodes.forEach(function (n) { if (n.nodeType === 1) mark(n); }); });
        }).observe(document.body, { childList: true, subtree: true });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start); else start();
})();
