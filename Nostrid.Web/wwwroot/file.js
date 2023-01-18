export function mountAndInitializeDb() {
    window.Module.FS.mkdir('/database');
    window.Module.FS.mount(window.Module.FS.filesystems.IDBFS, {}, '/database');
    return syncDatabase(true);
}

export function syncDatabase(populate) {

    return new Promise((resolve, reject) => {
        window.Module.FS.syncfs(populate, (err) => {
            if (err) {
                console.log('syncfs failed. Error:', err);
                reject(err);
            }
            else {
                console.log('synced successfull.');
                resolve();
            }
        });
    });
}