# Changelog

[_vNext_](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0...1.1.0) (2019-X-X)

## [1.0.0-rc7](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0-rc6...1.0.0-rc7) (2020-01-17)

### Features

- **client:** Add logs on `OnConnectedAsync`

## [1.0.0-rc6](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0-rc5...1.0.0-rc6) (2019-12-11)

### Bug Fixes

- **client:** only clean Client Grain state when `hub-disconnect` gracefully, otherwise don't so it might be possible to recover grain - if for some reason the hub connection is still active but the grain doesn't
- **client:** avoid reusing stream subscription handlers - attempt fix for `Handle is no longer valid. It has been used to unsubscribe or resume.`

## [1.0.0-rc5](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0-rc4...1.0.0-rc5) (2019-12-05)

### Bug Fixes

- **server directory:** dispose timer on deactivate
- **connection:** cleanup streams via timer instead of deferred timer (since it was timing out PubSub) and not triggering deactivation on connection remove
- **connection:** cleanup streams subscriptions when resume
- **client:** cleanup streams subscriptions when resume

## [1.0.0-rc4](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0-rc3...1.0.0-rc4) (2019-12-04)

### Bug Fixes

- **connection:** fix key parsing when key was containing `:` group was incorrect

## [1.0.0-rc3](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0-rc2...1.0.0-rc3) (2019-12-03)

### Bug Fixes

- **client:** handle subscribe correctly for `server-disconnected`
- **connection:** remove `Task` pooling and instead use one way invokes - was noticing timeouts stating after 30sec before 30sec (e.g. ~100ms) in several cases
- **connection:** on client disconnect defer `Remove` which potentially avoiding deadlock

### BREAKING CHANGES

- **connection:** `Send*` will not await a response and now its fire and forget

## [1.0.0-rc2](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0-rc1...1.0.0-rc2) (2019-11-22)

### Features

- **perf:** streams sharding for sending messages to servers - Should fix issue when having high throughput to a specific server it starts timing out

## [2.0.0-rc3](https://github.com/sketch7/SignalR.Orleans/compare/2.0.0-rc2...2.0.0-rc3) (2019-11-21)

### Features

- **perf:** streams sharding for sending messages to servers - Should fix issue when having high throughput to a specific server it starts timing out

## 2.0.0-rc2 (2019-11-21)

### Features

- **netcore:** netcore3 support

## 1.0.0-rc1 (2019-11-21)

### Features

- **netcore:** netcore2 support synced with 2.x

## 1.0.0-dev4 (2019-11-19)

### Features

- **netcore:** down version to netcore2.0