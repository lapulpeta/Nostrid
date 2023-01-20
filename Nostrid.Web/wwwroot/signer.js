export async function getPublicKey() {
    if (!window.nostr || !window.nostr.getPublicKey)
        return null;
    return await window.nostr.getPublicKey();
}

export async function signEvent(eventStr) {
    event = JSON.parse(eventStr)
    event = await window.nostr.signEvent(event);
    return JSON.stringify(event);
}