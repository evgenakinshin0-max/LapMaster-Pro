// stopwatch_java.java — спортивный секундомер на Java (Swing)

import javax.swing.*;
import javax.swing.table.*;
import java.awt.*;
import java.awt.event.*;
import java.io.*;
import java.nio.file.*;
import java.util.*;
import java.util.List;
import java.time.*;
import javax.sound.sampled.*;

public class StopwatchJava extends JFrame {
    private static final String SETTINGS_FILE = "stopwatch_settings.txt";
    private JLabel timeLabel;
    private JLabel bestLabel, worstLabel, avgLabel, countLabel;
    private JTable table;
    private DefaultTableModel tableModel;
    private JButton startBtn, stopBtn, lapBtn, resetBtn;
    private JLabel statusLabel;
    private javax.swing.Timer updateTimer;

    private boolean running = false;
    private boolean paused = false;
    private long startTime = 0;
    private long elapsed = 0;
    private List<Long> laps = new ArrayList<>();
    private long lapStart = 0;
    private boolean beepOnLap = true;
    private String soundFile = "default";

    public StopwatchJava() {
        setTitle("🏃 LapMaster Pro — Java");
        setSize(750, 600);
        setDefaultCloseOperation(EXIT_ON_CLOSE);
        setLayout(new BorderLayout());

        loadSettings();
        createUI();
        updateInfo();
    }

    private void createUI() {
        // Дисплей
        JPanel north = new JPanel();
        timeLabel = new JLabel("00:00:00.000");
        timeLabel.setFont(new Font("Arial", Font.BOLD, 48));
        north.add(timeLabel);
        add(north, BorderLayout.NORTH);

        // Кнопки
        JPanel center = new JPanel(new BorderLayout());
        JPanel btnPanel = new JPanel(new FlowLayout());
        startBtn = new JButton("Старт");
        stopBtn = new JButton("Стоп");
        lapBtn = new JButton("Круг");
        resetBtn = new JButton("Сброс");
        startBtn.setBackground(Color.GREEN);
        stopBtn.setBackground(Color.RED);
        btnPanel.add(startBtn);
        btnPanel.add(stopBtn);
        btnPanel.add(lapBtn);
        btnPanel.add(resetBtn);
        center.add(btnPanel, BorderLayout.NORTH);

        // Информация
        JPanel infoPanel = new JPanel(new FlowLayout());
        bestLabel = new JLabel("Лучший: --");
        worstLabel = new JLabel("Худший: --");
        avgLabel = new JLabel("Средний: --");
        countLabel = new JLabel("Кругов: 0");
        infoPanel.add(bestLabel);
        infoPanel.add(worstLabel);
        infoPanel.add(avgLabel);
        infoPanel.add(countLabel);
        center.add(infoPanel, BorderLayout.CENTER);

        // Таблица
        String[] cols = {"№", "Время круга", "Отставание", "Скорость (км/ч)"};
        tableModel = new DefaultTableModel(cols, 0);
        table = new JTable(tableModel);
        table.setRowHeight(25);
        JScrollPane scroll = new JScrollPane(table);
        center.add(scroll, BorderLayout.SOUTH);
        add(center, BorderLayout.CENTER);

        // Экспорт и настройки
        JPanel south = new JPanel(new FlowLayout());
        JButton exportBtn = new JButton("Экспорт CSV");
        JButton settingsBtn = new JButton("Настройки");
        south.add(exportBtn);
        south.add(settingsBtn);
        add(south, BorderLayout.SOUTH);

        // Статус
        statusLabel = new JLabel("Готов");
        add(statusLabel, BorderLayout.SOUTH);

        // Обработчики
        startBtn.addActionListener(e -> start());
        stopBtn.addActionListener(e -> stop());
        lapBtn.addActionListener(e -> lap());
        resetBtn.addActionListener(e -> reset());
        exportBtn.addActionListener(e -> exportCSV());
        settingsBtn.addActionListener(e -> settingsDialog());

        // Горячие клавиши
        KeyStroke space = KeyStroke.getKeyStroke(KeyEvent.VK_SPACE, 0);
        getRootPane().registerKeyboardAction(e -> startStopToggle(), space, JComponent.WHEN_IN_FOCUSED_WINDOW);
        KeyStroke enter = KeyStroke.getKeyStroke(KeyEvent.VK_ENTER, 0);
        getRootPane().registerKeyboardAction(e -> lap(), enter, JComponent.WHEN_IN_FOCUSED_WINDOW);
        KeyStroke r = KeyStroke.getKeyStroke(KeyEvent.VK_R, 0);
        getRootPane().registerKeyboardAction(e -> reset(), r, JComponent.WHEN_IN_FOCUSED_WINDOW);

        stopBtn.setEnabled(false);
        lapBtn.setEnabled(false);

        // Таймер
        updateTimer = new javax.swing.Timer(20, e -> updateTime());
    }

    private void start() {
        if (!running) {
            if (paused) {
                running = true;
                paused = false;
                startTime = System.currentTimeMillis() - elapsed;
                statusLabel.setText("Возобновлён");
            } else {
                running = true;
                paused = false;
                startTime = System.currentTimeMillis();
                elapsed = 0;
                laps.clear();
                lapStart = 0;
                refreshTable();
                statusLabel.setText("Запущен");
            }
            startBtn.setEnabled(false);
            stopBtn.setEnabled(true);
            lapBtn.setEnabled(true);
            updateTimer.start();
        }
    }

    private void stop() {
        if (running) {
            running = false;
            paused = true;
            startBtn.setEnabled(true);
            startBtn.setText("Возобновить");
            stopBtn.setEnabled(false);
            lapBtn.setEnabled(false);
            statusLabel.setText("На паузе");
            updateTimer.stop();
        }
    }

    private void startStopToggle() {
        if (running) stop();
        else start();
    }

    private void lap() {
        if (running) {
            long now = System.currentTimeMillis();
            long lapTime;
            if (lapStart == 0) {
                lapTime = now - startTime;
                lapStart = startTime;
            } else {
                lapTime = now - lapStart;
            }
            laps.add(lapTime);
            lapStart = now;
            if (beepOnLap) {
                playSound();
            }
            refreshTable();
            updateInfo();
            statusLabel.setText("Круг " + laps.size() + " зафиксирован");
        }
    }

    private void reset() {
        running = false;
        paused = false;
        elapsed = 0;
        laps.clear();
        lapStart = 0;
        startBtn.setEnabled(true);
        startBtn.setText("Старт");
        stopBtn.setEnabled(false);
        lapBtn.setEnabled(false);
        timeLabel.setText("00:00:00.000");
        refreshTable();
        updateInfo();
        statusLabel.setText("Сброшено");
        updateTimer.stop();
    }

    private void updateTime() {
        if (running) {
            long now = System.currentTimeMillis();
            elapsed = now - startTime;
            timeLabel.setText(formatTime(elapsed));
        }
    }

    private String formatTime(long ms) {
        long hours = ms / 3600000;
        long minutes = (ms % 3600000) / 60000;
        long seconds = (ms % 60000) / 1000;
        long millis = ms % 1000;
        return String.format("%02d:%02d:%02d.%03d", hours, minutes, seconds, millis);
    }

    private String formatTimeShort(long ms) {
        long minutes = ms / 60000;
        long seconds = (ms % 60000) / 1000;
        long millis = ms % 1000;
        return String.format("%02d:%02d.%03d", minutes, seconds, millis);
    }

    private void refreshTable() {
        tableModel.setRowCount(0);
        if (laps.isEmpty()) return;
        long best = Collections.min(laps);
        for (int i = 0; i < laps.size(); i++) {
            long t = laps.get(i);
            long diff = t - best;
            String diffStr = diff > 0 ? "+" + formatTimeShort(diff) : "-";
            tableModel.addRow(new Object[]{i+1, formatTimeShort(t), diffStr, "0.0"});
        }
    }

    private void updateInfo() {
        if (!laps.isEmpty()) {
            long best = Collections.min(laps);
            long worst = Collections.max(laps);
            double avg = laps.stream().mapToLong(Long::longValue).average().orElse(0);
            bestLabel.setText("Лучший: " + formatTimeShort(best));
            worstLabel.setText("Худший: " + formatTimeShort(worst));
            avgLabel.setText("Средний: " + formatTimeShort((long)avg));
            countLabel.setText("Кругов: " + laps.size());
        } else {
            bestLabel.setText("Лучший: --");
            worstLabel.setText("Худший: --");
            avgLabel.setText("Средний: --");
            countLabel.setText("Кругов: 0");
        }
    }

    private void playSound() {
        try {
            if (!soundFile.equals("default") && new File(soundFile).exists()) {
                AudioInputStream audioIn = AudioSystem.getAudioInputStream(new File(soundFile));
                Clip clip = AudioSystem.getClip();
                clip.open(audioIn);
                clip.start();
            } else {
                Toolkit.getDefaultToolkit().beep();
            }
        } catch (Exception e) {
            Toolkit.getDefaultToolkit().beep();
        }
    }

    private void exportCSV() {
        if (laps.isEmpty()) {
            JOptionPane.showMessageDialog(this, "Нет кругов для экспорта");
            return;
        }
        JFileChooser chooser = new JFileChooser();
        if (chooser.showSaveDialog(this) == JFileChooser.APPROVE_OPTION) {
            File file = chooser.getSelectedFile();
            try (PrintWriter out = new PrintWriter(file)) {
                out.println("Круг,Время(мс),Время(формат)");
                for (int i = 0; i < laps.size(); i++) {
                    out.println((i+1) + "," + laps.get(i) + "," + formatTimeShort(laps.get(i)));
                }
                statusLabel.setText("Экспортировано в " + file.getName());
            } catch (IOException e) {
                JOptionPane.showMessageDialog(this, "Ошибка экспорта");
            }
        }
    }

    private void settingsDialog() {
        JDialog dialog = new JDialog(this, "Настройки", true);
        dialog.setLayout(new GridLayout(0,1));
        JCheckBox beepBox = new JCheckBox("Включить звук при круге", beepOnLap);
        dialog.add(beepBox);
        JPanel soundPanel = new JPanel(new FlowLayout());
        soundPanel.add(new JLabel("Файл звука:"));
        JTextField soundField = new JTextField(soundFile, 20);
        soundPanel.add(soundField);
        JButton browseBtn = new JButton("Обзор...");
        browseBtn.addActionListener(e -> {
            JFileChooser chooser = new JFileChooser();
            if (chooser.showOpenDialog(dialog) == JFileChooser.APPROVE_OPTION) {
                soundField.setText(chooser.getSelectedFile().getAbsolutePath());
            }
        });
        soundPanel.add(browseBtn);
        dialog.add(soundPanel);
        JButton okBtn = new JButton("OK");
        okBtn.addActionListener(e -> {
            beepOnLap = beepBox.isSelected();
            soundFile = soundField.getText();
            saveSettings();
            dialog.dispose();
        });
        dialog.add(okBtn);
        dialog.pack();
        dialog.setLocationRelativeTo(this);
        dialog.setVisible(true);
    }

    private void loadSettings() {
        try {
            List<String> lines = Files.readAllLines(Paths.get(SETTINGS_FILE));
            if (lines.size() >= 2) {
                beepOnLap = Boolean.parseBoolean(lines.get(0));
                soundFile = lines.get(1);
            }
        } catch (IOException e) {}
    }

    private void saveSettings() {
        try {
            Files.write(Paths.get(SETTINGS_FILE), Arrays.asList(
                String.valueOf(beepOnLap),
                soundFile
            ));
        } catch (IOException e) {}
    }

    public static void main(String[] args) throws Exception {
        UIManager.setLookAndFeel(UIManager.getSystemLookAndFeelClassName());
        SwingUtilities.invokeLater(() -> new StopwatchJava().setVisible(true));
    }
}
