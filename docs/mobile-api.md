# کتابچهٔ API (`/api/v1`)

پایه: `http://localhost:5000` (توسعه). مستندات خودکار فقط در Development: `/openapi/v1.json`.
همهٔ ورودی/خروجی‌ها JSON با `Content-Type: application/json` هستند.
ورود فقط با نام کاربری/ایمیل و رمز عبور است؛ ثبت‌نام خودکار وجود ندارد
(کاربران را ادمین از صفحه «مدیریت کاربران» می‌سازد).

## ورود و گرفتن توکن

```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"username":"ali.m","password":"...","deviceName":"..."}'
# 200 {"token":"pat_...","expiresAtUtc":"..."}
# 401 — نام کاربری/رمز اشتباه یا حساب قفل‌شده
```

## استفاده از توکن

متن خام توکن **فقط یک‌بار** دیده می‌شود؛ ذخیره‌اش کنید:

```bash
TOKEN='pat_...'
curl http://localhost:5000/api/v1/profile \
  -H "Authorization: Bearer $TOKEN"
# 200 {"userName":"...","phoneNumber":"...","fullName":"...","email":"...","hasPassword":true}
```

## مدیریت نشست‌ها

```bash
# فهرست توکن‌های فعال من
curl http://localhost:5000/api/v1/auth/tokens -H "Authorization: Bearer $TOKEN"

# لغو یکی
curl -X DELETE http://localhost:5000/api/v1/auth/tokens/7 -H "Authorization: Bearer $TOKEN"
# 204 — موفق | 404 — مال من نیست

# خروج از همهٔ دستگاه‌ها
curl -X DELETE http://localhost:5000/api/v1/auth/tokens -H "Authorization: Bearer $TOKEN"
# 200 {"revoked":2}
```

## توزیع بسته (اپ توزیع‌کننده)

توکن را با همان `POST /api/v1/auth/login` (نام کاربری/رمز توزیع‌کننده) بگیرید:

```bash
# بسته‌های تحویل‌نشدهٔ من
curl http://localhost:5000/api/v1/distribution/pending -H "Authorization: Bearer $TOKEN"
# 200 [{"id":1,"city":"تهران","deliveryCode":"PKG-001","nationalCode":"...","status":0,...}]

# ثبت تحویل (هر دو از اسکن بارکد گیرنده می‌آیند)
curl -X POST http://localhost:5000/api/v1/distribution/deliver \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"deliveryCode":"PKG-001","nationalCode":"0023456789"}'
# 200 {"message":"تحویل ثبت شد.",...} — کد تحویل و کد ملی با پنل یکی بود
# 404 — بسته‌ای با این کد تحویل نیست
# 409 — قبلاً تحویل شده (زمان و تحویل‌دهنده برمی‌گردد)
# 403 — بسته به شما منتسب نیست
# 400 — کد ملی با پنل یکسان نیست
```

## پرداخت

```bash
# شروع پرداخت (مبلغ به تومان) → آدرس درگاه در مرورگر باز شود
curl -X POST http://localhost:5000/api/v1/payments \
  -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' \
  -d '{"amountTomans":50000,"description":"..."}'
# 200 {"paymentId":12,"paymentUrl":"https://www.zarinpal.com/pg/StartPay/..."}
# برگشت درگاه به /payments/callback می‌آید و به /payment-result هدایت می‌شود
```

## اعلان‌ها

```bash
# ۲۰ اعلان آخر + تعداد خوانده‌نشده
curl http://localhost:5000/api/v1/notifications/recent -H "Authorization: Bearer $TOKEN"

# خوانده‌شدن یکی / همه
curl -X POST http://localhost:5000/api/v1/notifications/5/read -H "Authorization: Bearer $TOKEN"
curl -X POST http://localhost:5000/api/v1/notifications/read-all -H "Authorization: Bearer $TOKEN"
```

## وب‌پوش

وب‌پوش مخصوص مرورگر است، نه اپ نیتیو.
کلید عمومی (بدون احراز هویت): `GET /api/v1/push/public-key`

## خطاها و محدودیت‌ها

| وضعیت | معنی |
|---|---|
| `400` با `{"message":"..."}` | خطای قابل‌انتظار؛ پیام فارسی را مستقیم نشان بدهید |
| `401` | توکن نیست/نامعتبر/منقضی/لغوشده → دوباره وارد شوید |
| `404` | منبع مال شما نیست یا نیست |
| `429` | محدودیت نرخ — `Retry-After` را رعایت کنید |

نکتهٔ امنیتی: توکن را هرگز در کد، لاگ یا گیت نگه ندارید؛ انقضای پیش‌فرض ۱۸۰ روز
(`ApiTokens:LifetimeDays`) و لغو تکی همیشه ممکن است.
