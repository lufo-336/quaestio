import { dotnet } from './_framework/dotnet.js';

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .withConfig({ loadAllSatelliteResources: true })
    .create();

dotnetRuntime.setModuleImports("quaestio-host", {
    isTouchDevice: () => window.matchMedia("(pointer: coarse)").matches || window.innerWidth < 768,
    loadStorageJson: () => {
        try {
            const out = {};
            const prefix = "wsaquiz:";
            for (let i = 0; i < window.localStorage.length; i++) {
                const key = window.localStorage.key(i);
                if (key && key.startsWith(prefix)) {
                    out[key.substring(prefix.length)] = window.localStorage.getItem(key);
                }
            }
            return JSON.stringify(out);
        } catch {
            return "{}";
        }
    },
    saveStorageFile: (path, content) => {
        try {
            window.localStorage.setItem("wsaquiz:" + path, content);
        } catch { }
    },
    removeStorageFile: (path) => {
        try {
            window.localStorage.removeItem("wsaquiz:" + path);
        } catch { }
    }
});

const config = dotnetRuntime.getConfig();
document.querySelector('.quaestio-splash')?.remove();
await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
