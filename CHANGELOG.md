# Changelog

[_vNext_](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0...1.1.0) (2019-X-X)

## [1.0.0-rc3](https://github.com/sketch7/SignalR.Orleans/compare/1.0.0-rc1...1.0.0-rc2) (2019-12-03)

### Bug Fixes

- **client:** handle subscribe correctly for `server-disconnected`
- **connection:** remove `Task` pooling and instead use one way invokes - was noticing timeouts stating after 30sec before 30sec (e.g. ~100ms) in several cases

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