export function setTheme(theme) {
    var attribute = "data-bs-theme";
    if (theme !== null) {
        document.body.setAttribute(attribute, theme);
    }
    return document.body.getAttribute(attribute);
}

export function getCssVariable(name) {
    return getComputedStyle(document.body)
        .getPropertyValue(name)
        .replace(RegExp("^#([0-9a-fA-F])([0-9a-fA-F])([0-9a-fA-F])$"), "#$1$1$2$2$3$3")
}

export function hideOffcanvasMenu() {
    if (!bootstrap) return;
    var menu = document.getElementById("offcanvasMenu");
    if (!menu) return;
    var instance = bootstrap.Offcanvas.getInstance(menu);
    if (!instance) return;
    instance.hide();
}

export function showModal(element) {
    if (!bootstrap) return;
    var modal = bootstrap.Modal.getOrCreateInstance(element.firstChild);
    if (!modal) return;
    modal.show();
}

export function hideModal(element) {
    if (!bootstrap) return;
    var modal = bootstrap.Modal.getOrCreateInstance(element.firstChild);
    if (!modal) return;
    modal.hide();
}

export function isModalShown(element) {
    if (!bootstrap) return;
    var modal = bootstrap.Modal.getOrCreateInstance(element.firstChild);
    if (!modal) return;
    return modal._isShown;
}

export function isElementVisible(element) {
    var position = element.getBoundingClientRect();
    return position.bottom > 0 && window.innerHeight > position.top;
}

export function createIntersectionObserver(element, componentInstance, methodName, margin) {
    const observer = new IntersectionObserver(async (entries) => {
        for (const entry of entries) {
            await componentInstance.invokeMethodAsync(methodName, entry.isIntersecting);
        }
    }, {
        root: findClosestScrollContainer(element),
        rootMargin: margin,
        threshold: 0,
    });
    observer.observe(element);
    return { dispose: () => observer.disconnect() };
}

export function scrollTop(element, value) {
    var elementWithScroll = findClosestScrollContainer(element);
    if (elementWithScroll) {
        elementWithScroll.scroll({
            top: value,
            behavior: 'smooth'
        });
    }
}

var findClosestScrollContainer = function (element) {
    while (element) {
        if (getComputedStyle(element).overflowY !== 'visible') {
            return element;
        }
        element = element.parentElement;
    }
    return null;
};

export function getAverageRGB(img) {
    var context = document.createElement('canvas').getContext('2d');
    context.imageSmoothingEnabled = true;
    context.drawImage(img, 0, 0, 1, 1);
    return context.getImageData(0, 0, 1, 1).data.slice(0, 3).join(",");
}

(() => {
    const tooltipTriggerList = document.querySelectorAll('[data-bs-toggle="tooltip"]')
    const tooltipList = [...tooltipTriggerList].map(tooltipTriggerEl => new bootstrap.Tooltip(tooltipTriggerEl))
})();

export async function encryptAes(plaintext, key, iv) {
    const rawkey = await window.crypto.subtle.importKey(
        "raw",
        key,
        "AES-CBC",
        true,
        ["encrypt", "decrypt"]
    );

    const encoder = new TextEncoder();
    const decrypted = encoder.encode(plaintext);

    const encrypted = await window.crypto.subtle.encrypt(
        {
            name: "AES-CBC",
            iv: iv
        },
        rawkey,
        decrypted
    );
    return new Uint8Array(encrypted);
}

export async function decryptAes(ciphertext, key, iv) {
    const rawkey = await window.crypto.subtle.importKey(
        "raw",
        key,
        "AES-CBC",
        true,
        ["encrypt", "decrypt"]
    );
    const decrypted = await window.crypto.subtle.decrypt(
        {
            name: "AES-CBC",
            iv: iv
        },
        rawkey,
        ciphertext
    );
    const decoder = new TextDecoder();
    const plaintext = decoder.decode(decrypted);
    return plaintext;
}
