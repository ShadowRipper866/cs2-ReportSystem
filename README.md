# CS 2 ReportSystem

**Простой плагин, позволяющий отправлять репорты с сервера в дискорд**

---

## Dependencies

[Metamod](https://www.sourcemm.net/downloads.php?branch=dev)

[CSSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases/latest)

---

## Config

```json
{ // Настройки локализации доступны в lang/ru(en).json
  "WebhookUrl": "https://discord.com/api/webhooks/....", // Ссылка на вебхук
  "FlagsToIgnore": [ "@css/root", "@css/ban"], // Если игрок имеет 1 из указанных флагов - на него нельзя отправить репорт (дэбаг позволяет кидать репорты на всех)
  "UseCenterHtmlMenu": true, // Использовать HTML Меню или чат-меню
  "SelectedColor": "#DC143C", // цвет вебхука
  "Description": "1243524", // сообщение вебхука
  "DefaultReasons": [
    "reason",
    "da",
    "12345"
  ],
  "Debug": true, // Позволяет включить режим дэбага, который добавляет возможность отправить репорт на самого себя, а так же на тех, у кого есть флаги из FlagsToIgnore
  "ServerIP": "0.0.0.0:20000",
  "ConfigVersion": 1
}
```
