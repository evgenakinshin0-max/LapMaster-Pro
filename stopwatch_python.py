

### 1. `stopwatch_python.py`

```python
# stopwatch_python.py — спортивный секундомер на Python (Tkinter)

import tkinter as tk
from tkinter import ttk, messagebox, filedialog
import time
import threading
import json
import os
import csv
from playsound import playsound

class Stopwatch:
    def __init__(self, root):
        self.root = root
        self.root.title("🏃 LapMaster Pro — Python")
        self.root.geometry("700x550")
        self.root.resizable(True, True)

        # Состояние
        self.running = False
        self.paused = False
        self.start_time = 0
        self.elapsed_time = 0  # в миллисекундах
        self.laps = []  # список времён кругов в мс
        self.lap_start = 0
        self.beep_on_lap = tk.BooleanVar(value=True)
        self.sound_file = "default"

        # GUI
        self.create_widgets()
        self.update_display()
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

    def create_widgets(self):
        # Главный дисплей
        self.time_label = tk.Label(self.root, font=("Arial", 48), text="00:00:00.000")
        self.time_label.pack(pady=20)

        # Кнопки
        btn_frame = tk.Frame(self.root)
        btn_frame.pack(pady=10)
        self.start_btn = tk.Button(btn_frame, text="Старт", command=self.start, width=10, bg="green", fg="white")
        self.start_btn.pack(side=tk.LEFT, padx=5)
        self.stop_btn = tk.Button(btn_frame, text="Стоп", command=self.stop, width=10, bg="red", fg="white")
        self.stop_btn.pack(side=tk.LEFT, padx=5)
        self.lap_btn = tk.Button(btn_frame, text="Круг", command=self.lap, width=10, bg="orange")
        self.lap_btn.pack(side=tk.LEFT, padx=5)
        self.reset_btn = tk.Button(btn_frame, text="Сброс", command=self.reset, width=10, bg="gray")
        self.reset_btn.pack(side=tk.LEFT, padx=5)

        # Информация о кругах
        info_frame = tk.Frame(self.root)
        info_frame.pack(pady=5, fill=tk.X)
        self.best_label = tk.Label(info_frame, text="Лучший: --")
        self.best_label.pack(side=tk.LEFT, padx=10)
        self.worst_label = tk.Label(info_frame, text="Худший: --")
        self.worst_label.pack(side=tk.LEFT, padx=10)
        self.avg_label = tk.Label(info_frame, text="Средний: --")
        self.avg_label.pack(side=tk.LEFT, padx=10)
        self.count_label = tk.Label(info_frame, text="Кругов: 0")
        self.count_label.pack(side=tk.LEFT, padx=10)

        # Таблица кругов
        self.tree = ttk.Treeview(self.root, columns=("num", "time", "diff", "speed"), show="headings", height=12)
        self.tree.heading("num", text="№")
        self.tree.heading("time", text="Время круга")
        self.tree.heading("diff", text="Отставание")
        self.tree.heading("speed", text="Скорость (км/ч)")
        self.tree.column("num", width=50)
        self.tree.column("time", width=150)
        self.tree.column("diff", width=120)
        self.tree.column("speed", width=100)
        self.tree.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        # Экспорт и настройки
        export_frame = tk.Frame(self.root)
        export_frame.pack(pady=5)
        tk.Button(export_frame, text="Экспорт CSV", command=self.export_csv).pack(side=tk.LEFT, padx=5)
        tk.Button(export_frame, text="Настройки", command=self.settings_dialog).pack(side=tk.LEFT, padx=5)

        # Статус
        self.status = tk.Label(self.root, text="Готов", anchor=tk.W)
        self.status.pack(fill=tk.X, padx=10)

        # Горячие клавиши
        self.root.bind("<space>", lambda e: self.start_stop_toggle())
        self.root.bind("<Return>", lambda e: self.lap())
        self.root.bind("<r>", lambda e: self.reset())

    def start(self):
        if not self.running:
            if self.paused:
                # Возобновление
                self.running = True
                self.paused = False
                self.start_time = time.time() * 1000 - self.elapsed_time
                self.status.config(text="Возобновлён")
            else:
                # Первый старт
                self.running = True
                self.paused = False
                self.start_time = time.time() * 1000
                self.elapsed_time = 0
                self.laps = []
                self.lap_start = 0
                self.refresh_laps()
                self.status.config(text="Запущен")
            self.start_btn.config(text="Старт", state=tk.DISABLED)
            self.stop_btn.config(text="Стоп", state=tk.NORMAL)
            self.lap_btn.config(state=tk.NORMAL)
            self.update_loop()

    def stop(self):
        if self.running:
            self.running = False
            self.paused = True
            self.start_btn.config(text="Возобновить", state=tk.NORMAL)
            self.stop_btn.config(state=tk.DISABLED)
            self.lap_btn.config(state=tk.DISABLED)
            self.status.config(text="На паузе")

    def start_stop_toggle(self):
        if self.running:
            self.stop()
        else:
            self.start()

    def lap(self):
        if self.running:
            now = time.time() * 1000
            if self.lap_start == 0:
                lap_time = now - self.start_time
                self.lap_start = self.start_time
            else:
                lap_time = now - self.lap_start
            self.laps.append(lap_time)
            self.lap_start = now
            # Звук
            if self.beep_on_lap.get():
                if self.sound_file != "default" and os.path.exists(self.sound_file):
                    playsound(self.sound_file)
                else:
                    # Встроенный beep через print
                    print('\a', end='', flush=True)
            self.refresh_laps()
            self.update_info()
            self.status.config(text=f"Круг {len(self.laps)} зафиксирован")

    def reset(self):
        self.running = False
        self.paused = False
        self.elapsed_time = 0
        self.laps = []
        self.lap_start = 0
        self.start_btn.config(text="Старт", state=tk.NORMAL)
        self.stop_btn.config(state=tk.DISABLED)
        self.lap_btn.config(state=tk.DISABLED)
        self.time_label.config(text="00:00:00.000")
        self.refresh_laps()
        self.update_info()
        self.status.config(text="Сброшено")

    def update_loop(self):
        if self.running:
            now = time.time() * 1000
            self.elapsed_time = now - self.start_time
            self.time_label.config(text=self.format_time(self.elapsed_time))
            self.root.after(20, self.update_loop)  # 20 мс

    def format_time(self, ms):
        hours = int(ms // 3600000)
        minutes = int((ms % 3600000) // 60000)
        seconds = int((ms % 60000) // 1000)
        millis = int(ms % 1000)
        return f"{hours:02d}:{minutes:02d}:{seconds:02d}.{millis:03d}"

    def format_time_short(self, ms):
        minutes = int(ms // 60000)
        seconds = int((ms % 60000) // 1000)
        millis = int(ms % 1000)
        return f"{minutes:02d}:{seconds:02d}.{millis:03d}"

    def refresh_laps(self):
        for row in self.tree.get_children():
            self.tree.delete(row)
        total_laps = len(self.laps)
        best = min(self.laps) if self.laps else 0
        for i, t in enumerate(self.laps, 1):
            diff = t - best if self.laps else 0
            diff_str = f"+{self.format_time_short(diff)}" if diff > 0 else "-"
            # Скорость (фиктивная, если не вводить дистанцию)
            speed = 0
            # для красоты можно добавить фиктивную скорость
            self.tree.insert("", "end", values=(i, self.format_time_short(t), diff_str, f"{speed:.1f}"))

    def update_info(self):
        if self.laps:
            best = min(self.laps)
            worst = max(self.laps)
            avg = sum(self.laps) / len(self.laps)
            self.best_label.config(text=f"Лучший: {self.format_time_short(best)}")
            self.worst_label.config(text=f"Худший: {self.format_time_short(worst)}")
            self.avg_label.config(text=f"Средний: {self.format_time_short(avg)}")
            self.count_label.config(text=f"Кругов: {len(self.laps)}")
        else:
            self.best_label.config(text="Лучший: --")
            self.worst_label.config(text="Худший: --")
            self.avg_label.config(text="Средний: --")
            self.count_label.config(text="Кругов: 0")

    def export_csv(self):
        if not self.laps:
            messagebox.showinfo("Нет данных", "Нет кругов для экспорта")
            return
        file_path = filedialog.asksaveasfilename(defaultextension=".csv", filetypes=[("CSV files", "*.csv")])
        if file_path:
            with open(file_path, 'w', newline='') as f:
                writer = csv.writer(f)
                writer.writerow(["Круг", "Время (мс)", "Время (формат)"])
                for i, t in enumerate(self.laps, 1):
                    writer.writerow([i, t, self.format_time_short(t)])
            self.status.config(text=f"Экспортировано в {os.path.basename(file_path)}")

    def settings_dialog(self):
        dialog = tk.Toplevel(self.root)
        dialog.title("Настройки")
        dialog.geometry("300x150")
        tk.Label(dialog, text="Звук при круге:").pack(pady=5)
        cb = ttk.Checkbutton(dialog, text="Включить", variable=self.beep_on_lap)
        cb.pack(pady=5)
        tk.Label(dialog, text="Файл звука (оставьте default для системного):").pack(pady=5)
        entry = tk.Entry(dialog, width=30)
        entry.insert(0, self.sound_file)
        entry.pack(pady=5)
        def save_sound():
            self.sound_file = entry.get().strip()
            dialog.destroy()
        tk.Button(dialog, text="Сохранить", command=save_sound).pack(pady=10)

    def on_close(self):
        self.root.destroy()

if __name__ == "__main__":
    root = tk.Tk()
    app = Stopwatch(root)
    root.mainloop()
