// stopwatch_rs.rs — спортивный секундомер на Rust (консоль с termion)

use std::io::{self, Write, BufRead};
use std::time::{Duration, Instant};
use std::thread;
use chrono::Utc;
use termion::{color, style, clear, cursor};

struct Stopwatch {
    running: bool,
    paused: bool,
    start_time: Option<Instant>,
    elapsed: u128, // миллисекунды
    laps: Vec<u128>,
    lap_start: Option<Instant>,
    beep_on_lap: bool,
    sound_file: String,
}

impl Stopwatch {
    fn new() -> Self {
        Stopwatch {
            running: false,
            paused: false,
            start_time: None,
            elapsed: 0,
            laps: Vec::new(),
            lap_start: None,
            beep_on_lap: true,
            sound_file: "default".to_string(),
        }
    }

    fn start(&mut self) {
        if !self.running {
            if self.paused {
                self.running = true;
                self.paused = false;
                let now = Utc::now().timestamp_millis();
                // Для точности используем Instant, но для простоты используем продолжительность
                // Пересчёт: мы не сохраняем точное время, но можно сделать через Instant
                // Используем Instant для отсчёта
                if let Some(start) = self.start_time {
                    // Мы не можем восстановить, поэтому перезапускаем с учётом elapsed
                    self.start_time = Some(Instant::now() - Duration::from_millis(self.elapsed as u64));
                }
                println!("Возобновлён");
            } else {
                self.running = true;
                self.paused = false;
                self.start_time = Some(Instant::now());
                self.elapsed = 0;
                self.laps.clear();
                self.lap_start = None;
                println!("Запущен");
            }
        }
    }

    fn stop(&mut self) {
        if self.running {
            self.running = false;
            self.paused = true;
            // Сохраняем elapsed
            if let Some(start) = self.start_time {
                self.elapsed = start.elapsed().as_millis();
            }
            println!("На паузе");
        }
    }

    fn lap(&mut self) {
        if self.running {
            let now = Instant::now();
            let lap_time = if let Some(lap_start) = self.lap_start {
                lap_start.elapsed().as_millis()
            } else if let Some(start) = self.start_time {
                start.elapsed().as_millis()
            } else {
                0
            };
            self.laps.push(lap_time);
            self.lap_start = Some(now);
            if self.beep_on_lap {
                print!("\x07");
                io::stdout().flush().unwrap();
            }
            println!("Круг {} зафиксирован ({})", self.laps.len(), format_time_short(lap_time));
        }
    }

    fn reset(&mut self) {
        self.running = false;
        self.paused = false;
        self.start_time = None;
        self.elapsed = 0;
        self.laps.clear();
        self.lap_start = None;
        println!("Сброшено");
    }

    fn show_laps(&self) {
        if self.laps.is_empty() {
            println!("Нет кругов");
            return;
        }
        let best = *self.laps.iter().min().unwrap();
        println!("Круги:");
        for (i, &t) in self.laps.iter().enumerate() {
            let diff = t - best;
            let diff_str = if diff > 0 { format!("+{}", format_time_short(diff)) } else { "-".to_string() };
            println!("  {}. {}  (отст. {})", i+1, format_time_short(t), diff_str);
        }
    }

    fn show_stats(&self) {
        if self.laps.is_empty() {
            println!("Нет данных");
            return;
        }
        let best = *self.laps.iter().min().unwrap();
        let worst = *self.laps.iter().max().unwrap();
        let sum: u128 = self.laps.iter().sum();
        let avg = sum as f64 / self.laps.len() as f64;
        println!("Лучший: {}", format_time_short(best));
        println!("Худший: {}", format_time_short(worst));
        println!("Средний: {}", format_time_short(avg as u128));
        println!("Кругов: {}", self.laps.len());
    }

    fn export_csv(&self) {
        if self.laps.is_empty() {
            println!("Нет кругов для экспорта");
            return;
        }
        let file_name = "laps_export.csv";
        let mut content = "Круг,Время(мс),Время(формат)\n".to_string();
        for (i, &t) in self.laps.iter().enumerate() {
            content.push_str(&format!("{},{},{}\n", i+1, t, format_time_short(t)));
        }
        if let Ok(_) = std::fs::write(file_name, content) {
            println!("Экспортировано в {}", file_name);
        } else {
            println!("Ошибка экспорта");
        }
    }
}

fn format_time(ms: u128) -> String {
    let hours = ms / 3600000;
    let minutes = (ms % 3600000) / 60000;
    let seconds = (ms % 60000) / 1000;
    let millis = ms % 1000;
    format!("{:02}:{:02}:{:02}.{:03}", hours, minutes, seconds, millis)
}

fn format_time_short(ms: u128) -> String {
    let minutes = ms / 60000;
    let seconds = (ms % 60000) / 1000;
    let millis = ms % 1000;
    format!("{:02}:{:02}.{:03}", minutes, seconds, millis)
}

fn main() {
    let mut sw = Stopwatch::new();
    let stdin = io::stdin();
    let mut reader = stdin.lock();

    println!("🏃 LapMaster Pro — Rust Edition");
    println!("Команды: start, stop, lap, reset, laps, stats, export, settings, exit");
    println!("Сокращения: s - start/stop, l - lap, r - reset");

    // Фоновый поток для обновления времени
    let sw_ref = std::cell::RefCell::new(sw);
    thread::spawn(move || {
        loop {
            thread::sleep(Duration::from_millis(20));
            let mut sw = sw_ref.borrow_mut();
            if sw.running {
                if let Some(start) = sw.start_time {
                    let elapsed = start.elapsed().as_millis();
                    // Вывод в ту же строку
                    print!("\r{}{}Текущее время: {}  {}", clear::AfterCursor, cursor::Goto(1, 1), format_time(elapsed), color::Fg(color::Reset));
                    io::stdout().flush().unwrap();
                }
            }
        }
    });

    loop {
        print!("{}> ", color::Fg(color::Cyan));
        io::stdout().flush().unwrap();
        let mut line = String::new();
        if reader.read_line(&mut line).is_err() { break; }
        let line = line.trim();
        let parts: Vec<&str> = line.splitn(2, ' ').collect();
        let cmd = parts[0];
        let mut sw = sw_ref.borrow_mut();
        match cmd {
            "start" => sw.start(),
            "stop" => sw.stop(),
            "s" => {
                if sw.running { sw.stop() } else { sw.start() }
            }
            "lap" | "l" => sw.lap(),
            "reset" | "r" => sw.reset(),
            "laps" => sw.show_laps(),
            "stats" => sw.show_stats(),
            "export" => sw.export_csv(),
            "settings" => {
                print!("Включать звук при круге (y/n): ");
                io::stdout().flush().unwrap();
                let mut ans = String::new();
                reader.read_line(&mut ans).unwrap();
                sw.beep_on_lap = ans.trim() == "y";
                print!("Файл звука (оставьте default): ");
                io::stdout().flush().unwrap();
                let mut file = String::new();
                reader.read_line(&mut file).unwrap();
                sw.sound_file = file.trim().to_string();
            }
            "exit" => {
                println!("До свидания!");
                break;
            }
            _ => println!("Неизвестная команда"),
        }
    }
}
