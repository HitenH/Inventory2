
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
