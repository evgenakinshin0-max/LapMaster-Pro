// stopwatch_js.js — спортивный секундомер на JavaScript (Electron)

const { app, BrowserWindow, Menu, dialog, ipcMain } = require('electron');
const settings = require('electron-settings');
const fs = require('fs');
const { exec } = require('child_process');

let mainWindow;
let stopwatch = {
    running: false,
    paused: false,
    startTime: 0,
    elapsed: 0,
    laps: [],
    lapStart: 0,
    beepOnLap: true,
    soundFile: 'default'
};
let updateInterval = null;

function loadSettings() {
    const data = settings.getSync('stopwatch') || {};
    stopwatch.beepOnLap = data.beepOnLap !== undefined ? data.beepOnLap : true;
    stopwatch.soundFile = data.soundFile || 'default';
    stopwatch.laps = data.laps || [];
    stopwatch.elapsed = data.elapsed || 0;
    stopwatch.running = false;
    stopwatch.paused = false;
    stopwatch.startTime = 0;
    stopwatch.lapStart = 0;
}

function saveSettings() {
    settings.setSync('stopwatch', {
        beepOnLap: stopwatch.beepOnLap,
        soundFile: stopwatch.soundFile,
        laps: stopwatch.laps,
        elapsed: stopwatch.elapsed
    });
}

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 750,
        height: 600,
        webPreferences: {
            nodeIntegration: true,
            contextIsolation: false
        }
    });
    mainWindow.loadFile('index.html');
    Menu.setApplicationMenu(Menu.buildFromTemplate([
        { label: 'Файл', submenu: [{ role: 'quit' }] }
    ]));

    loadSettings();

    // IPC handlers
    ipcMain.handle('get-state', () => {
        return {
            time: stopwatch.elapsed,
            laps: stopwatch.laps,
            running: stopwatch.running,
            paused: stopwatch.paused,
            beepOnLap: stopwatch.beepOnLap,
            soundFile: stopwatch.soundFile
        };
    });

    ipcMain.handle('start', () => {
        if (!stopwatch.running) {
            if (stopwatch.paused) {
                stopwatch.running = true;
                stopwatch.paused = false;
                stopwatch.startTime = Date.now() - stopwatch.elapsed;
            } else {
                stopwatch.running = true;
                stopwatch.paused = false;
                stopwatch.startTime = Date.now();
                stopwatch.elapsed = 0;
                stopwatch.laps = [];
                stopwatch.lapStart = 0;
            }
            startUpdater();
            saveSettings();
        }
        return getState();
    });

    ipcMain.handle('stop', () => {
        if (stopwatch.running) {
            stopwatch.running = false;
            stopwatch.paused = true;
            stopwatch.elapsed = Date.now() - stopwatch.startTime;
            stopUpdater();
            saveSettings();
        }
        return getState();
    });

    ipcMain.handle('lap', () => {
        if (stopwatch.running) {
            const now = Date.now();
            let lapTime;
            if (stopwatch.lapStart === 0) {
                lapTime = now - stopwatch.startTime;
                stopwatch.lapStart = stopwatch.startTime;
            } else {
                lapTime = now - stopwatch.lapStart;
            }
            stopwatch.laps.push(lapTime);
            stopwatch.lapStart = now;
            if (stopwatch.beepOnLap) playSound();
            saveSettings();
            mainWindow.webContents.send('lap-added', stopwatch.laps.length, lapTime);
        }
        return getState();
    });

    ipcMain.handle('reset', () => {
        stopwatch.running = false;
        stopwatch.paused = false;
        stopwatch.elapsed = 0;
        stopwatch.laps = [];
        stopwatch.lapStart = 0;
        stopwatch.startTime = 0;
        stopUpdater();
        saveSettings();
        return getState();
    });

    ipcMain.handle('export-csv', () => {
        if (stopwatch.laps.length === 0) {
            dialog.showMessageBox(mainWindow, { message: 'Нет кругов для экспорта' });
            return;
        }
        dialog.showSaveDialog(mainWindow, { filters: [{ name: 'CSV', extensions: ['csv'] }] })
            .then(result => {
                if (!result.canceled) {
                    let content = 'Круг,Время(мс),Время(формат)\n';
                    stopwatch.laps.forEach((t, i) => {
                        content += `${i+1},${t},${formatTimeShort(t)}\n`;
                    });
                    fs.writeFileSync(result.filePath, content);
                }
            });
    });

    ipcMain.handle('settings', (event, beep, sound) => {
        stopwatch.beepOnLap = beep;
        stopwatch.soundFile = sound;
        saveSettings();
    });

    function getState() {
        return {
            time: stopwatch.elapsed,
            laps: stopwatch.laps,
            running: stopwatch.running,
            paused: stopwatch.paused
        };
    }

    function startUpdater() {
        if (updateInterval) clearInterval(updateInterval);
        updateInterval = setInterval(() => {
            if (stopwatch.running) {
                stopwatch.elapsed = Date.now() - stopwatch.startTime;
                mainWindow.webContents.send('time-update', stopwatch.elapsed);
            }
        }, 20);
    }

    function stopUpdater() {
        if (updateInterval) {
            clearInterval(updateInterval);
            updateInterval = null;
        }
    }

    function playSound() {
        if (stopwatch.soundFile !== 'default' && fs.existsSync(stopwatch.soundFile)) {
            const cmd = process.platform === 'win32' ? 'start' : (process.platform === 'darwin' ? 'afplay' : 'aplay');
            exec(`${cmd} "${stopwatch.soundFile}"`);
        } else {
            // system beep via console
            process.stdout.write('\x07');
        }
    }

    function formatTimeShort(ms) {
        const minutes = Math.floor(ms / 60000);
        const seconds = Math.floor((ms % 60000) / 1000);
        const millis = ms % 1000;
        return `${minutes.toString().padStart(2,'0')}:${seconds.toString().padStart(2,'0')}.${millis.toString().padStart(3,'0')}`;
    }

    mainWindow.on('closed', () => {
        mainWindow = null;
        stopUpdater();
    });
}

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') app.quit();
});
