// MockHttpClient.h
// Test-only HttpClient subclass that records requests and returns
// canned responses. Used by testyoutubeprovider (and any future
// provider test that needs to assert on the request shape) so the
// tests can run fully offline.
//
// Usage:
//   MockHttpClient http;
//   http.enqueueResponse("youtubei/v1/player", HttpResponse{200, json, {}, {}});
//   http.enqueueResponse("youtube.com/s/player/", HttpResponse{200, js, {}, {}});
//
//   // … use http anywhere a HttpClient* is expected.
//
//   QVERIFY(http.requests().first().url.contains("player?video_id=…"));
//
// `enqueueResponse` matches by URL substring. If no queued response
// matches, the callback is invoked with an error message and the
// request is still recorded. The callback is dispatched via
// QTimer::singleShot(0) so it runs on the next event-loop tick,
// matching the real client's async semantics.

#pragma once

#include "HttpClient.h"

#include <QList>
#include <QMutex>
#include <QString>

#include <memory>

namespace mf::core::test {

class MockHttpClient : public mf::core::sources::HttpClient {
    Q_OBJECT
public:
    struct RecordedRequest {
        QString                       url;
        QByteArray                    method;
        QHash<QString, QString>       headers;
        QByteArray                    body;
    };

    explicit MockHttpClient(QObject* parent = nullptr);
    ~MockHttpClient() override;

    void enqueueResponse(const QString& urlSubstring,
                         mf::core::sources::HttpResponse resp);
    void enqueueError(const QString& urlSubstring, const QString& errorMessage);
    void enqueueStatus(const QString& urlSubstring,
                       int statusCode,
                       const QByteArray& body = QByteArray());

    QList<RecordedRequest> requests() const;
    QList<RecordedRequest> requestsMatching(const QString& urlSubstring) const;
    void                   clearRequests();
    int                    queuedCount() const;
    bool                   isQueueEmpty() const;

    // Test helpers: synchronously run the event loop until the queue
    // drains or `timeoutMs` elapses. Returns the number of responses
    // that were actually delivered.
    int drain(int timeoutMs = 2000);

protected:
    QString send(const mf::core::sources::HttpRequest& req,
                 mf::core::sources::ResponseCallback cb) override;

private:
    struct Queued {
        QString  urlSubstring;
        mf::core::sources::HttpResponse resp;
    };

    bool popMatching(const QString& url, Queued& out);

    mutable QMutex    mtx_;
    QList<Queued>            queue_;
    QList<RecordedRequest>   recorded_;
    quint64                  nextTag_ = 1;
    int                      inFlight_ = 0;
};

} // namespace mf::core::test
