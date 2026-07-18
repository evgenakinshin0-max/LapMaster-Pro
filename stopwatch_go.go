// stopwatch_go.go — спортивный секундомер на Go (консоль с цветами)

package main

import (
	"bufio"
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"
)

type Stopwatch struct {
	running    bool
	paused     bool
	startTime  int64
	elapsed    int64
	laps       []int64
	lapStart   int64
	beepOnLap  bool
	soundFile  string
}

func NewStopwatch() *Stopwatch {
	return &Stopwatch{
		beepOnLap: true,
		soundFile: "default",
	}
}

func (s *Stopwatch) Start() {
	if !s.running {
		if s.paused {
			s.running = true
			s.paused = false
			s.startTime = time.Now().UnixMilli() - s.elapsed
			fmt.Println("Возобновлён")
		} else {
			s.running = true
			s.paused = false
			s.startTime = time.Now().UnixMilli()
			s.elapsed = 0
			s.laps = []int64{}
			s.lapStart = 0
			fmt.Println("Запущен")
		}
	}
}

func (s *Stopwatch) Stop() {
	if s.running {
		s.running = false
		s.paused = true
		fmt.Println("На паузе")
	}
}

func (s *Stopwatch) Lap() {
	if s.running {
		now := time.Now().UnixMilli()
		var lapTime int64
		if s.lapStart == 0 {
			lapTime = now - s.startTime
			s.lapStart = s.startTime
		} else {
			lapTime = now - s.lapStart
		}
		s.laps = append(s.laps, lapTime)
		s.lapStart = now
		if s.beepOnLap {
			fmt.Print("\a") // beep
		}
		fmt.Printf("Круг %d зафиксирован (%s)\n", len(s.laps), formatTimeShort(lapTime))
	}
}

func (s *Stopwatch) Reset() {
	s.running = false
	s.paused = false
	s.elapsed = 0
	s.laps = []int64{}
	s.lapStart = 0
	fmt.Println("Сброшено")
}

func (s *Stopwatch) ShowLaps() {
	if len(s.laps) == 0 {
		fmt.Println("Нет кругов")
		return
	}
	best := s.laps[0]
	for _, v := range s.laps {
		if v < best {
			best = v
		}
	}
	fmt.Println("Круги:")
	for i, t := range s.laps {
		diff := t - best
		diffStr := "-"
		if diff > 0 {
			diffStr = "+" + formatTimeShort(diff)
		}
		fmt.Printf("  %d. %s  (отст. %s)\n", i+1, formatTimeShort(t), diffStr)
	}
}

func (s *Stopwatch) ShowStats() {
	if len(s.laps) == 0 {
		fmt.Println("Нет данных")
		return
	}
	best, worst := s.laps[0], s.laps[0]
	sum := int64(0)
	for _, v := range s.laps {
		if v < best {
			best = v
		}
		if v > worst {
			worst = v
		}
		sum += v
	}
	avg := float64(sum) / float64(len(s.laps))
	fmt.Printf("Лучший: %s\n", formatTimeShort(best))
	fmt.Printf("Худший: %s\n", formatTimeShort(worst))
	fmt.Printf("Средний: %s\n", formatTimeShort(int64(avg)))
	fmt.Printf("Кругов: %d\n", len(s.laps))
}

func (s *Stopwatch) ExportCSV() {
	if len(s.laps) == 0 {
		fmt.Println("Нет кругов для экспорта")
		return
	}
	fileName := "laps_export.csv"
	file, err := os.Create(fileName)
	if err != nil {
		fmt.Println("Ошибка создания файла")
		return
	}
	defer file.Close()
	file.WriteString("Круг,Время(мс),Время(формат)\n")
	for i, t := range s.laps {
		file.WriteString(fmt.Sprintf("%d,%d,%s\n", i+1, t, formatTimeShort(t)))
	}
	fmt.Printf("Экспортировано в %s\n", fileName)
}

func formatTime(ms int64) string {
	hours := ms / 3600000
	minutes := (ms % 3600000) / 60000
	seconds := (ms % 60000) / 1000
	millis := ms % 1000
	return fmt.Sprintf("%02d:%02d:%02d.%03d", hours, minutes, seconds, millis)
}

func formatTimeShort(ms int64) string {
	minutes := ms / 60000
	seconds := (ms % 60000) / 1000
	millis := ms % 1000
	return fmt.Sprintf("%02d:%02d.%03d", minutes, seconds, millis)
}

func main() {
	sw := NewStopwatch()
	scanner := bufio.NewScanner(os.Stdin)
	fmt.Println("🏃 LapMaster Pro — Go Edition")
	fmt.Println("Команды: start, stop, lap, reset, laps, stats, export, settings, exit")
	fmt.Println("Также можно использовать: s - start/stop, l - lap, r - reset")

	// Фоновый поток для отображения времени (непрерывно)
	go func() {
		for {
			if sw.running {
				now := time.Now().UnixMilli()
				elapsed := now - sw.startTime
				// выводим текущее время в ту же строку (используем управляющие последовательности)
				fmt.Printf("\rТекущее время: %s   ", formatTime(elapsed))
			}
			time.Sleep(20 * time.Millisecond)
		}
	}()

	for {
		fmt.Print("\n> ")
		if !scanner.Scan() {
			break
		}
		line := strings.TrimSpace(scanner.Text())
		parts := strings.SplitN(line, " ", 2)
		cmd := parts[0]
		arg := ""
		if len(parts) > 1 {
			arg = parts[1]
		}
		switch cmd {
		case "start":
			sw.Start()
		case "stop":
			sw.Stop()
		case "s":
			if sw.running {
				sw.Stop()
			} else {
				sw.Start()
			}
		case "lap", "l":
			sw.Lap()
		case "reset", "r":
			sw.Reset()
			fmt.Print("\r") // очищаем строку времени
		case "laps":
			sw.ShowLaps()
		case "stats":
			sw.ShowStats()
		case "export":
			sw.ExportCSV()
		case "settings":
			fmt.Print("Включать звук при круге (y/n): ")
			scanner.Scan()
			ans := strings.TrimSpace(scanner.Text())
			sw.beepOnLap = (ans == "y")
			fmt.Print("Файл звука (оставьте default): ")
			scanner.Scan()
			sw.soundFile = strings.TrimSpace(scanner.Text())
		case "exit":
			fmt.Println("До свидания!")
			return
		default:
			fmt.Println("Неизвестная команда")
		}
	}
}
