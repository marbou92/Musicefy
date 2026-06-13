// HttpClient.h
// Thin wrapper around QNetworkAccessManager with:
//   - cookie persistence (QNetworkCookieJar)
//   - automatic redirect following
//   - configurable timeout
//   - user-agent
//   - response body returned as QByteArray
//   - convenience for JSON / text responses
//
// All operations are async and use QNetworkReply* signals. The client
// itself is a QObject so it can be moved to a worker thread if needed.

#pragma once

#include <QByteArray>
#include <QHash>
#include <QList>
#include <QNetworkAccessManager>
#include <QNetworkCookieJar>
#include <QNetworkRequest>
#include <QObject>
#include <QString>
#include <QTimer>

#include <functional>
#include <memory>

namespace mf::core::sources {

struct HttpResponse {
    int         statusCode = 0;
    QByteArray  body;
    QHash<QString, QString> headers;
    QString     errorMessage;
    bool        ok() const { return statusCode >= 200 && statusCode < 300 && errorMessage.isEmpty(); }
};

struct HttpRequest {
    QString                                 url;
    QByteArray                              method = "GET";
    QHash<QString, QString>                 headers;
    QByteArray                              body;
    QString                                 contentType; // optional override
    int                                     timeoutMs = 30000;
    bool                                    followRedirects = true;
};

class HttpClient : public QObject {
    Q_OBJECT
public:
    explicit HttpClient(QObject* parent = nullptr);
    ~HttpClient() override;
    HttpClient(const HttpClient&) = delete;
    HttpClient& operator=(const HttpClient&) = delete;

    // Returns a tag used to cancel the request later.
    using ResponseCallback = std::function<void(HttpResponse)>;

    QString get(const HttpRequest& req, ResponseCallback cb);
    QString post(const HttpRequest& req, ResponseCallback cb);
    virtual QString send(const HttpRequest& req, ResponseCallback cb);
    void    cancel(QString tag);

    void setDefaultUserAgent(QString ua);
    QString defaultUserAgent() const { return defaultUserAgent_; }

    void setCookieJar(QNetworkCookieJar* jar);
    QNetworkCookieJar* cookieJar() const { return manager_.cookieJar(); }

signals:
    void requestStarted(QString tag, QString url);
    void requestFinished(QString tag, int statusCode, qint64 bytesReceived);
    void requestFailed(QString tag, QString error);

private:
    struct Pending {
        HttpRequest      req;
        ResponseCallback cb;
        QTimer*          timeout = nullptr;
        QNetworkReply*   reply   = nullptr;
    };

    QNetworkAccessManager manager_;
    QString               defaultUserAgent_;
    quint64               nextTag_ = 1;
    QHash<quint64, Pending*> pending_;
};

} // namespace mf::core::sources