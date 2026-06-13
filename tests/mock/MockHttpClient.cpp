// MockHttpClient.cpp — see MockHttpClient.h.

#include "MockHttpClient.h"

#include <QCoreApplication>
#include <QElapsedTimer>
#include <QEventLoop>
#include <QMutexLocker>
#include <QTimer>
#include <QTimerEvent>
#include <QUrl>

#include <utility>

namespace mf::core::test {

MockHttpClient::MockHttpClient(QObject* parent)
    : mf::core::sources::HttpClient(parent) {
}

MockHttpClient::~MockHttpClient() = default;

void MockHttpClient::enqueueResponse(const QString& urlSubstring,
                                     mf::core::sources::HttpResponse resp) {
    QMutexLocker lock(&mtx_);
    Queued q;
    q.urlSubstring = urlSubstring;
    q.resp         = std::move(resp);
    queue_.append(std::move(q));
}

void MockHttpClient::enqueueError(const QString& urlSubstring,
                                  const QString& errorMessage) {
    mf::core::sources::HttpResponse r;
    r.statusCode   = 0;
    r.errorMessage = errorMessage;
    enqueueResponse(urlSubstring, std::move(r));
}

void MockHttpClient::enqueueStatus(const QString& urlSubstring,
                                   int statusCode,
                                   const QByteArray& body) {
    mf::core::sources::HttpResponse r;
    r.statusCode = statusCode;
    r.body       = body;
    enqueueResponse(urlSubstring, std::move(r));
}

QList<MockHttpClient::RecordedRequest> MockHttpClient::requests() const {
    QMutexLocker lock(&mtx_);
    return recorded_;
}

QList<MockHttpClient::RecordedRequest>
MockHttpClient::requestsMatching(const QString& urlSubstring) const {
    QMutexLocker lock(&mtx_);
    QList<RecordedRequest> out;
    for (const auto& r : recorded_) {
        if (r.url.contains(urlSubstring)) out.append(r);
    }
    return out;
}

void MockHttpClient::clearRequests() {
    QMutexLocker lock(&mtx_);
    recorded_.clear();
}

int MockHttpClient::queuedCount() const {
    QMutexLocker lock(&mtx_);
    return queue_.size();
}

bool MockHttpClient::isQueueEmpty() const {
    QMutexLocker lock(&mtx_);
    return queue_.isEmpty();
}

bool MockHttpClient::popMatching(const QString& url, Queued& out) {
    QMutexLocker lock(&mtx_);
    for (int i = 0; i < queue_.size(); ++i) {
        if (url.contains(queue_[i].urlSubstring)) {
            out = queue_.takeAt(i);
            return true;
        }
    }
    return false;
}

QString MockHttpClient::send(const mf::core::sources::HttpRequest& req,
                             mf::core::sources::ResponseCallback cb) {
    // Record synchronously so tests can inspect requests immediately
    // even before the response fires.
    {
        QMutexLocker lock(&mtx_);
        RecordedRequest r;
        r.url     = req.url;
        r.method  = req.method;
        r.headers = req.headers;
        r.body    = req.body;
        recorded_.append(std::move(r));
        ++inFlight_;
    }

    const quint64 tag = nextTag_++;
    const QString tagStr = QString::number(tag);

    Queued matched;
    const bool found = popMatching(req.url, matched);

    QTimer::singleShot(0, this, [this, cb = std::move(cb), found, matched,
                                 tagStr]() mutable {
        mf::core::sources::HttpResponse resp;
        if (found) {
            resp = std::move(matched.resp);
        } else {
            resp.statusCode   = 0;
            resp.errorMessage = QStringLiteral("MockHttpClient: no canned response queued for %1")
                                    .arg(QString::fromLatin1("<no-match>"));
            // Overwrite the placeholder above with the real URL for
            // clearer test output.
            // (We don't have the URL in this closure; rely on the
            // recordedRequests list to debug.)
        }
        {
            QMutexLocker lock(&mtx_);
            --inFlight_;
        }
        if (cb) cb(std::move(resp));
    });

    return tagStr;
}

int MockHttpClient::drain(int timeoutMs) {
    QElapsedTimer t;
    t.start();
    while (t.elapsed() < timeoutMs) {
        {
            QMutexLocker lock(&mtx_);
            if (inFlight_ == 0 && queue_.isEmpty()) break;
        }
        QCoreApplication::processEvents(QEventLoop::AllEvents, 50);
    }
    {
        QMutexLocker lock(&mtx_);
        return inFlight_;
    }
}

} // namespace mf::core::test
