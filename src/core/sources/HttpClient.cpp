// HttpClient.cpp
// See HttpClient.h for design notes.

#include "HttpClient.h"

#include <QNetworkCookie>
#include <QNetworkCookieJar>
#include <QNetworkReply>
#include <QNetworkRequest>
#include <QUrl>

#include <utility>

namespace mf::core::sources {

HttpClient::HttpClient(QObject* parent)
    : QObject(parent)
    , defaultUserAgent_(QStringLiteral("Musicefy/2.0 (Qt; Windows)"))
{
    manager_.setCookieJar(new QNetworkCookieJar(this));
    manager_.setTransferTimeout(60'000); // 60s hard ceiling
    manager_.setRedirectPolicy(QNetworkRequest::NoLessSafeRedirectPolicy);
}

HttpClient::~HttpClient() {
    for (auto it = pending_.begin(); it != pending_.end(); ++it) {
        if (it.value() && it.value()->reply) {
            it.value()->reply->abort();
        }
        delete it.value();
    }
    pending_.clear();
}

QString HttpClient::send(const HttpRequest& req, ResponseCallback cb) {
    quint64 tag = nextTag_++;
    auto pending = new Pending();
    pending->req = req;
    pending->cb  = std::move(cb);

    QNetworkRequest nreq{QUrl(req.url)};
    nreq.setRawHeader("User-Agent", defaultUserAgent_.toUtf8());
    nreq.setAttribute(QNetworkRequest::RedirectPolicyAttribute,
                      req.followRedirects
                          ? QNetworkRequest::NoLessSafeRedirectPolicy
                          : QNetworkRequest::ManualRedirectPolicy);
    for (auto it = req.headers.constBegin(); it != req.headers.constEnd(); ++it) {
        nreq.setRawHeader(it.key().toUtf8(), it.value().toUtf8());
    }
    if (!req.contentType.isEmpty()) {
        nreq.setHeader(QNetworkRequest::ContentTypeHeader, req.contentType);
    }
    if (req.timeoutMs > 0) {
        pending->timeout = new QTimer(this);
        pending->timeout->setSingleShot(true);
        pending->timeout->setInterval(req.timeoutMs);
    }

    QNetworkReply* reply = nullptr;
    QByteArray m = req.method.toUpper();
    if (m == "GET") {
        reply = manager_.get(nreq);
    } else if (m == "POST") {
        reply = manager_.post(nreq, req.body);
    } else if (m == "PUT") {
        reply = manager_.put(nreq, req.body);
    } else if (m == "DELETE") {
        reply = manager_.deleteResource(nreq);
    } else if (m == "HEAD") {
        reply = manager_.head(nreq);
    } else {
        // Custom verb.
        reply = manager_.sendCustomRequest(nreq, m, req.body);
    }
    pending->reply = reply;
    QString tagStr = QString::number(tag);
    reply->setProperty("musicefy_tag", tagStr);
    pending_.insert(tag, std::move(pending));

    connect(reply, &QNetworkReply::finished, this, [this, tag]() {
        auto it = pending_.find(tag);
        if (it == pending_.end()) return;
        Pending* p = it.value();
        HttpResponse resp;
        resp.statusCode = p->reply->error() == QNetworkReply::NoError
                              ? p->reply->attribute(QNetworkRequest::HttpStatusCodeAttribute).toInt()
                              : 0;
        if (resp.statusCode == 0 && p->reply->error() != QNetworkReply::NoError) {
            resp.errorMessage = p->reply->errorString();
        }
        resp.body = p->reply->readAll();
        for (const QPair<QByteArray, QByteArray>& h : p->reply->rawHeaderPairs()) {
            resp.headers.insert(QString::fromUtf8(h.first), QString::fromUtf8(h.second));
        }
        if (p->timeout) p->timeout->stop();
        ResponseCallback cb = std::move(p->cb);
        QNetworkReply* rep = p->reply;
        pending_.erase(it);
        if (cb) cb(resp);
        rep->deleteLater();
    });

    if (pending->timeout) {
        QTimer* t = pending->timeout;
        connect(t, &QTimer::timeout, this, [this, tag]() {
            auto it = pending_.find(tag);
            if (it == pending_.end()) return;
            Pending* p = it.value();
            if (p->reply) {
                p->reply->abort();
            }
        });
        t->start();
    }

    emit requestStarted(tagStr, req.url);
    return tagStr;
}

QString HttpClient::get(const HttpRequest& req, ResponseCallback cb) {
    HttpRequest r = req;
    r.method = "GET";
    return send(r, std::move(cb));
}

QString HttpClient::post(const HttpRequest& req, ResponseCallback cb) {
    HttpRequest r = req;
    r.method = "POST";
    return send(r, std::move(cb));
}

void HttpClient::cancel(QString tag) {
    bool ok;
    quint64 id = tag.toULongLong(&ok);
    if (!ok) return;
    auto it = pending_.find(id);
    if (it == pending_.end()) return;
    if (it.value()->reply) {
        it.value()->reply->abort();
    }
}

void HttpClient::setDefaultUserAgent(QString ua) {
    defaultUserAgent_ = std::move(ua);
}

void HttpClient::setCookieJar(QNetworkCookieJar* jar) {
    if (manager_.cookieJar() == jar) return;
    manager_.setCookieJar(jar);
}

} // namespace mf::core::sources