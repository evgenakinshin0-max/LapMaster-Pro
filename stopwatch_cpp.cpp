// stopwatch_cpp.cpp — спортивный секундомер на C++ (Qt Widgets)

#include <QApplication>
#include <QMainWindow>
#include <QWidget>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QPushButton>
#include <QTableWidget>
#include <QHeaderView>
#include <QMessageBox>
#include <QTimer>
#include <QDateTime>
#include <QFileDialog>
#include <QTextStream>
#include <QSound>
#include <QCheckBox>
#include <QSettings>

class Stopwatch : public QMainWindow {
    Q_OBJECT
public:
    Stopwatch(QWidget *parent = nullptr) : QMainWindow(parent) {
        setWindowTitle("🏃 LapMaster Pro — C++");
        resize(750, 600);
        createUI();
        loadSettings();
        updateInfo();
    }

private slots:
    void start() {
        if (!running) {
            if (paused) {
                running = true;
                paused = false;
                startTime = QDateTime::currentMSecsSinceEpoch() - elapsed;
                statusLabel->setText("Возобновлён");
            } else {
                running = true;
                paused = false;
                startTime = QDateTime::currentMSecsSinceEpoch();
                elapsed = 0;
                laps.clear();
                lapStart = 0;
                refreshTable();
                statusLabel->setText("Запущен");
            }
            startBtn->setEnabled(false);
            stopBtn->setEnabled(true);
            lapBtn->setEnabled(true);
            updateTimer->start(20);
        }
    }

    void stop() {
        if (running) {
            running = false;
            paused = true;
            startBtn->setEnabled(true);
            startBtn->setText("Возобновить");
            stopBtn->setEnabled(false);
            lapBtn->setEnabled(false);
            statusLabel->setText("На паузе");
            updateTimer->stop();
        }
    }

    void lap() {
        if (running) {
            qint64 now = QDateTime::currentMSecsSinceEpoch();
            qint64 lapTime;
            if (lapStart == 0) {
                lapTime = now - startTime;
                lapStart = startTime;
            } else {
                lapTime = now - lapStart;
            }
            laps.append(lapTime);
            lapStart = now;
            if (beepOnLap) {
                if (soundFile != "default" && QFile::exists(soundFile)) {
                    QSound::play(soundFile);
                } else {
                    QApplication::beep();
                }
            }
            refreshTable();
            updateInfo();
            statusLabel->setText(QString("Круг %1 зафиксирован").arg(laps.size()));
        }
    }

    void reset() {
        running = false;
        paused = false;
        elapsed = 0;
        laps.clear();
        lapStart = 0;
        startBtn->setEnabled(true);
        startBtn->setText("Старт");
        stopBtn->setEnabled(false);
        lapBtn->setEnabled(false);
        timeLabel->setText("00:00:00.000");
        refreshTable();
        updateInfo();
        statusLabel->setText("Сброшено");
        updateTimer->stop();
    }

    void updateTime() {
        if (running) {
            qint64 now = QDateTime::currentMSecsSinceEpoch();
            elapsed = now - startTime;
            timeLabel->setText(formatTime(elapsed));
        }
    }

    void exportCSV() {
        if (laps.isEmpty()) {
            QMessageBox::information(this, "Нет данных", "Нет кругов для экспорта");
            return;
        }
        QString fileName = QFileDialog::getSaveFileName(this, "Экспорт CSV", "", "CSV (*.csv)");
        if (!fileName.isEmpty()) {
            QFile file(fileName);
            if (file.open(QIODevice::WriteOnly | QIODevice::Text)) {
                QTextStream out(&file);
                out << "Круг,Время(мс),Время(формат)\n";
                for (int i = 0; i < laps.size(); ++i) {
                    out << i+1 << "," << laps[i] << "," << formatTimeShort(laps[i]) << "\n";
                }
                file.close();
                statusLabel->setText("Экспортировано в " + QFileInfo(fileName).fileName());
            }
        }
    }

    void settingsDialog() {
        // Упрощённо: диалог с чекбоксом и выбором файла
        QDialog dialog(this);
        dialog.setWindowTitle("Настройки");
        QVBoxLayout layout(&dialog);
        QCheckBox *beepBox = new QCheckBox("Включить звук при круге", &dialog);
        beepBox->setChecked(beepOnLap);
        layout.addWidget(beepBox);
        QHBoxLayout *soundLayout = new QHBoxLayout();
        soundLayout->addWidget(new QLabel("Файл звука:"));
        QLineEdit *soundEdit = new QLineEdit(soundFile);
        soundLayout->addWidget(soundEdit);
        QPushButton *browseBtn = new QPushButton("Обзор...");
        connect(browseBtn, &QPushButton::clicked, [=]() {
            QString file = QFileDialog::getOpenFileName(&dialog, "Выберите звук", "", "Audio (*.wav *.mp3)");
            if (!file.isEmpty()) soundEdit->setText(file);
        });
        soundLayout->addWidget(browseBtn);
        layout.addLayout(soundLayout);
        QPushButton *okBtn = new QPushButton("OK");
        connect(okBtn, &QPushButton::clicked, [&]() {
            beepOnLap = beepBox->isChecked();
            soundFile = soundEdit->text();
            dialog.accept();
        });
        layout.addWidget(okBtn);
        dialog.exec();
        saveSettings();
    }

private:
    QLabel *timeLabel;
    QLabel *bestLabel, *worstLabel, *avgLabel, *countLabel;
    QTableWidget *table;
    QPushButton *startBtn, *stopBtn, *lapBtn, *resetBtn;
    QLabel *statusLabel;
    QTimer *updateTimer;

    bool running = false;
    bool paused = false;
    qint64 startTime = 0;
    qint64 elapsed = 0;
    QList<qint64> laps;
    qint64 lapStart = 0;
    bool beepOnLap = true;
    QString soundFile = "default";

    void createUI() {
        QWidget *central = new QWidget(this);
        setCentralWidget(central);
        QVBoxLayout *mainLayout = new QVBoxLayout(central);

        // Дисплей
        timeLabel = new QLabel("00:00:00.000");
        timeLabel->setStyleSheet("font-size: 48px; font-weight: bold;");
        timeLabel->setAlignment(Qt::AlignCenter);
        mainLayout->addWidget(timeLabel);

        // Кнопки
        QHBoxLayout *btnLayout = new QHBoxLayout();
        startBtn = new QPushButton("Старт");
        stopBtn = new QPushButton("Стоп");
        lapBtn = new QPushButton("Круг");
        resetBtn = new QPushButton("Сброс");
        startBtn->setStyleSheet("background-color: green; color: white;");
        stopBtn->setStyleSheet("background-color: red; color: white;");
        btnLayout->addWidget(startBtn);
        btnLayout->addWidget(stopBtn);
        btnLayout->addWidget(lapBtn);
        btnLayout->addWidget(resetBtn);
        mainLayout->addLayout(btnLayout);
        connect(startBtn, &QPushButton::clicked, this, &Stopwatch::start);
        connect(stopBtn, &QPushButton::clicked, this, &Stopwatch::stop);
        connect(lapBtn, &QPushButton::clicked, this, &Stopwatch::lap);
        connect(resetBtn, &QPushButton::clicked, this, &Stopwatch::reset);

        // Информация
        QHBoxLayout *infoLayout = new QHBoxLayout();
        bestLabel = new QLabel("Лучший: --");
        worstLabel = new QLabel("Худший: --");
        avgLabel = new QLabel("Средний: --");
        countLabel = new QLabel("Кругов: 0");
        infoLayout->addWidget(bestLabel);
        infoLayout->addWidget(worstLabel);
        infoLayout->addWidget(avgLabel);
        infoLayout->addWidget(countLabel);
        mainLayout->addLayout(infoLayout);

        // Таблица
        table = new QTableWidget(0, 4);
        table->setHorizontalHeaderLabels({"№", "Время круга", "Отставание", "Скорость (км/ч)"});
        table->horizontalHeader()->setSectionResizeMode(QHeaderView::Stretch);
        mainLayout->addWidget(table);

        // Экспорт и настройки
        QHBoxLayout *bottomLayout = new QHBoxLayout();
        QPushButton *exportBtn = new QPushButton("Экспорт CSV");
        QPushButton *settingsBtn = new QPushButton("Настройки");
        bottomLayout->addWidget(exportBtn);
        bottomLayout->addWidget(settingsBtn);
        mainLayout->addLayout(bottomLayout);
        connect(exportBtn, &QPushButton::clicked, this, &Stopwatch::exportCSV);
        connect(settingsBtn, &QPushButton::clicked, this, &Stopwatch::settingsDialog);

        // Статус
        statusLabel = new QLabel("Готов");
        mainLayout->addWidget(statusLabel);

        // Таймер обновления
        updateTimer = new QTimer(this);
        connect(updateTimer, &QTimer::timeout, this, &Stopwatch::updateTime);

        // Горячие клавиши (упрощённо)
        // можно добавить через QShortcut, но опустим для краткости

        stopBtn->setEnabled(false);
        lapBtn->setEnabled(false);
        loadSettings();
    }

    QString formatTime(qint64 ms) {
        int hours = ms / 3600000;
        int minutes = (ms % 3600000) / 60000;
        int seconds = (ms % 60000) / 1000;
        int millis = ms % 1000;
        return QString("%1:%2:%3.%4")
                .arg(hours, 2, 10, QChar('0'))
                .arg(minutes, 2, 10, QChar('0'))
                .arg(seconds, 2, 10, QChar('0'))
                .arg(millis, 3, 10, QChar('0'));
    }

    QString formatTimeShort(qint64 ms) {
        int minutes = ms / 60000;
        int seconds = (ms % 60000) / 1000;
        int millis = ms % 1000;
        return QString("%1:%2.%3")
                .arg(minutes, 2, 10, QChar('0'))
                .arg(seconds, 2, 10, QChar('0'))
                .arg(millis, 3, 10, QChar('0'));
    }

    void refreshTable() {
        table->setRowCount(laps.size());
        qint64 best = laps.isEmpty() ? 0 : *std::min_element(laps.begin(), laps.end());
        for (int i = 0; i < laps.size(); ++i) {
            qint64 diff = laps[i] - best;
            QString diffStr = diff > 0 ? QString("+%1").arg(formatTimeShort(diff)) : "-";
            table->setItem(i, 0, new QTableWidgetItem(QString::number(i+1)));
            table->setItem(i, 1, new QTableWidgetItem(formatTimeShort(laps[i])));
            table->setItem(i, 2, new QTableWidgetItem(diffStr));
            table->setItem(i, 3, new QTableWidgetItem("0.0")); // скорость фиктивная
        }
    }

    void updateInfo() {
        if (!laps.isEmpty()) {
            qint64 best = *std::min_element(laps.begin(), laps.end());
            qint64 worst = *std::max_element(laps.begin(), laps.end());
            double avg = 0;
            for (qint64 v : laps) avg += v;
            avg /= laps.size();
            bestLabel->setText(QString("Лучший: %1").arg(formatTimeShort(best)));
            worstLabel->setText(QString("Худший: %1").arg(formatTimeShort(worst)));
            avgLabel->setText(QString("Средний: %1").arg(formatTimeShort((qint64)avg)));
            countLabel->setText(QString("Кругов: %1").arg(laps.size()));
        } else {
            bestLabel->setText("Лучший: --");
            worstLabel->setText("Худший: --");
            avgLabel->setText("Средний: --");
            countLabel->setText("Кругов: 0");
        }
    }

    void loadSettings() {
        QSettings settings("MyApp", "LapMaster");
        beepOnLap = settings.value("beepOnLap", true).toBool();
        soundFile = settings.value("soundFile", "default").toString();
    }

    void saveSettings() {
        QSettings settings("MyApp", "LapMaster");
        settings.setValue("beepOnLap", beepOnLap);
        settings.setValue("soundFile", soundFile);
    }
};

int main(int argc, char *argv[]) {
    QApplication app(argc, argv);
    Stopwatch w;
    w.show();
    return app.exec();
}

#include "stopwatch_cpp.moc"
