// LyricsPanel.h
// Bottom-sheet panel that displays embedded lyrics for the current track.
// Shows "Instrumental" placeholder when lyrics are empty.
// Slides up from bottom with animation.

#pragma once

#include <QWidget>
#include <QLabel>
#include <QScrollArea>
#include <QPropertyAnimation>

class QVBoxLayout;

namespace mf::app::viewmodels { class PlayerViewModel; }
namespace mf::core::theme    { class ThemeManager; }

namespace mf::app::widgets {

class LyricsPanel : public QWidget {
    Q_OBJECT
    Q_PROPERTY(int panelHeight READ panelHeight WRITE setPanelHeight NOTIFY panelHeightChanged)
public:
    LyricsPanel(mf::app::viewmodels::PlayerViewModel* vm,
                mf::core::theme::ThemeManager*        theme,
                QWidget* parent = nullptr);
    ~LyricsPanel() override = default;

    void toggle();
    void showPanel();
    void hidePanel();
    bool isShowing() const { return showing_; }

    int panelHeight() const { return maximumHeight(); }
    void setPanelHeight(int h) { setMaximumHeight(h); }

signals:
    void panelHeightChanged();
    void visibilityChanged(bool visible);

private slots:
    void onCurrentTrackChanged();
    void onThemeChanged();

private:
    void buildUi();
    void applyTheme();
    void updateLyrics();
    void animateShow();
    void animateHide();

    mf::app::viewmodels::PlayerViewModel* vm_    = nullptr;
    mf::core::theme::ThemeManager*        theme_ = nullptr;

    QScrollArea* scrollArea_ = nullptr;
    QWidget*     contentWidget_ = nullptr;
    QVBoxLayout* contentLayout_ = nullptr;
    QLabel*      titleLabel_ = nullptr;
    bool         showing_ = false;
    QPropertyAnimation* showAnim_ = nullptr;
    QPropertyAnimation* hideAnim_ = nullptr;
};

} // namespace mf::app::widgets
