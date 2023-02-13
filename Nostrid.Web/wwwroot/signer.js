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

export async function decryptNip04(pubkey, plaintext) {
    if (!window.nostr || !window.nostr.nip04 || !window.nostr.nip04.decrypt)
        return null;
    return await window.nostr.nip04.decrypt(pubkey, plaintext);
}

export async function encryptNip04(pubkey, plaintext) {
    if (!window.nostr || !window.nostr.nip04 || !window.nostr.nip04.encrypt)
        return null;
    return await window.nostr.nip04.encrypt(pubkey, plaintext);
}