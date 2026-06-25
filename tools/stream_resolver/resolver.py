import argparse
import asyncio
import contextlib
import io
import json
import os
import re
import sys
import uuid
import hashlib
import time
from pathlib import Path
from urllib.parse import parse_qs, urljoin, urlparse
from urllib.request import Request, urlopen


ROOT = Path(__file__).resolve().parent
DLR_ROOT = ROOT / "vendor" / "douyin_live_recorder"
QUALITY_ALIASES = {
    "0": "OD",
    "1": "BD",
    "2": "UHD",
    "3": "HD",
    "4": "SD",
    "5": "LD",
    "OD": "OD",
    "BD": "BD",
    "UHD": "UHD",
    "HD": "HD",
    "SD": "SD",
    "LD": "LD",
    "原画": "OD",
    "蓝光": "BD",
    "超清": "UHD",
    "高清": "HD",
    "标清": "SD",
    "流畅": "LD",
}

QUALITY_NAMES = {
    "OD": "原画",
    "BD": "蓝光",
    "UHD": "超清",
    "HD": "高清",
    "SD": "标清",
    "LD": "流畅",
}

OVERSEA_HINTS = (
    "tiktok.com",
    "sooplive",
    "pandalive",
    "winktv",
    "flextv",
    "ttinglive",
    "popkontv",
    "twitch.tv",
    "liveme.com",
    "showroom-live.com",
    "chzzk.naver.com",
    "shopee",
    "shp.ee",
    "youtube.com",
    "youtu.be",
    "faceit.com",
    "picarto.tv",
)

RECORD_HEADERS = {
    "PandaTV": "origin:https://www.pandalive.co.kr",
    "WinkTV": "origin:https://www.winktv.co.kr",
    "PopkonTV": "origin:https://www.popkontv.com",
    "FlexTV": "origin:https://www.flextv.co.kr",
    "Qiandurebo": "referer:https://qiandurebo.com",
    "17Live": "referer:https://17.live/en/live/6302408",
    "LangLive": "referer:https://www.lang.live",
    "Blued": "referer:https://app.blued.cn",
    "twitch": "User-Agent:Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36;Referer:https://www.twitch.tv/;Origin:https://www.twitch.tv",
    "Bilibili": "User-Agent:Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36;Referer:https://live.bilibili.com/;Origin:https://live.bilibili.com",
}


def normalize_url(url):
    value = (url or "").strip()
    if not value:
        return value
    if not re.match(r"^[a-zA-Z][a-zA-Z0-9+.-]*://", value):
        value = "https://" + value
    return value


def normalize_quality(value):
    key = str(value or "OD").strip().upper()
    return QUALITY_ALIASES.get(key, QUALITY_ALIASES.get(str(value or "").strip(), "OD"))


def display_quality(value):
    quality = normalize_quality(value)
    return QUALITY_NAMES.get(quality, str(value or "").strip() or QUALITY_NAMES["OD"])


def last_path(url):
    parsed = urlparse(url)
    parts = [part for part in parsed.path.split("/") if part]
    return parts[-1] if parts else parsed.netloc


def host_contains(url, value):
    return value in urlparse(url).netloc.lower()


def douyu_room_id(url):
    parsed = urlparse(url)
    query = parse_qs(parsed.query)
    for key in ("rid", "room_id", "roomId"):
        values = query.get(key)
        if values and values[0].strip():
            return values[0].strip()
    parts = [part for part in parsed.path.split("/") if part]
    if not parts:
        return parsed.netloc
    for part in reversed(parts):
        if part.lower() not in ("topic", "room", "share", "live"):
            return part
    return parts[-1]


def normalize_douyu_url(url):
    rid = douyu_room_id(url)
    return f"https://www.douyu.com/{rid}" if rid else url


def normalize_twitch_url(url):
    parsed = urlparse(url)
    parts = [part for part in parsed.path.split("/") if part]
    if not parts:
        return url
    return f"https://www.twitch.tv/{parts[0]}"


def load_config(path):
    if not path:
        path = ROOT / "resolver_config.json"
    else:
        path = Path(path)
    if path.exists():
        with path.open("r", encoding="utf-8") as file:
            return json.load(file)
    return {}


def get_nested(config, *keys, default=""):
    value = config
    for key in keys:
        if not isinstance(value, dict) or key not in value:
            return default
        value = value[key]
    return value if value is not None else default


def cookie(config, key, fallback):
    return get_nested(config, "cookies", key, default="") or fallback or ""


def account(config, section, key):
    return get_nested(config, "accounts", section, key, default="")


def token(config, key):
    return get_nested(config, "tokens", key, default="")


def is_match(url, *needles):
    return any(needle in url for needle in needles)


def walk_values(value):
    yield value
    if isinstance(value, dict):
        for item in value.values():
            yield from walk_values(item)
    elif isinstance(value, (list, tuple, set)):
        for item in value:
            yield from walk_values(item)


def first_text_by_keys(value, keys):
    if not isinstance(value, dict):
        return ""
    for key in keys:
        current = value.get(key)
        if current is None:
            continue
        if isinstance(current, (str, int, float)):
            text = str(current).strip()
            if text:
                return text
    for item in value.values():
        found = first_text_by_keys(item, keys)
        if found:
            return found
    return ""


def find_uid(url, info):
    found = first_text_by_keys(info, (
        "uid",
        "user_id",
        "userId",
        "unique_id",
        "uniqueId",
        "web_rid",
        "webRid",
        "room_id",
        "roomId",
        "anchor_id",
        "anchorId",
        "short_id",
        "shortId",
        "display_id",
        "displayId",
    ))
    return found or last_path(url)


def normalize_resolution_text(width, height):
    try:
        w = int(float(width))
        h = int(float(height))
    except (TypeError, ValueError):
        return ""
    if w <= 0 or h <= 0:
        return ""
    return f"{w}x{h}"


def find_resolution(value, urls=None):
    urls = urls or []
    if isinstance(value, dict):
        width = first_text_by_keys(value, ("width", "w", "video_width", "videoWidth"))
        height = first_text_by_keys(value, ("height", "h", "video_height", "videoHeight"))
        found = normalize_resolution_text(width, height)
        if found:
            return found

    for item in walk_values(value):
        if isinstance(item, str):
            match = re.search(r"(?<!\d)([1-9]\d{2,4})[xX*]([1-9]\d{2,4})(?!\d)", item)
            if match:
                return normalize_resolution_text(match.group(1), match.group(2))

    for url in urls:
        match = re.search(r"(?<!\d)([1-9]\d{2,4})[xX*]([1-9]\d{2,4})(?!\d)", url or "")
        if match:
            return normalize_resolution_text(match.group(1), match.group(2))
    return ""


def normalize_bitrate_text(value):
    if value is None:
        return ""
    text = str(value).strip()
    if not text:
        return ""
    match = re.search(r"([1-9]\d{2,8})(?:\.\d+)?", text)
    if not match:
        return text
    number = float(match.group(1))
    unit = text.lower()
    if "mbps" in unit or "m/s" in unit:
        return f"{number:g} Mbps"
    if "kbps" in unit or "k/s" in unit:
        return f"{number:g} kbps"
    if number >= 1000000:
        return f"{number / 1000000:g} Mbps"
    if number >= 100000:
        return f"{number / 1000:g} kbps"
    return f"{number:g} kbps"


def find_bitrate(value, urls=None):
    urls = urls or []
    if isinstance(value, dict):
        found = first_text_by_keys(value, (
            "bitrate",
            "bit_rate",
            "bitRate",
            "video_bitrate",
            "videoBitrate",
            "vbitrate",
            "v_bit_rate",
            "bandwidth",
        ))
        normalized = normalize_bitrate_text(found)
        if normalized:
            return normalized

    for item in walk_values(value):
        if isinstance(item, str):
            parsed = urlparse(item)
            query = parse_qs(parsed.query)
            for key in ("bitrate", "bit_rate", "br", "vbitrate", "v_bit_rate", "bandwidth"):
                if key in query and query[key]:
                    normalized = normalize_bitrate_text(query[key][0])
                    if normalized:
                        return normalized
            match = re.search(r"(?:bitrate|vbitrate|bandwidth|br)[=_-]([1-9]\d{2,8})", item, re.IGNORECASE)
            if match:
                normalized = normalize_bitrate_text(match.group(1))
                if normalized:
                    return normalized

    for url in urls:
        parsed = urlparse(url or "")
        query = parse_qs(parsed.query)
        for key in ("bitrate", "bit_rate", "br", "vbitrate", "v_bit_rate", "bandwidth"):
            if key in query and query[key]:
                normalized = normalize_bitrate_text(query[key][0])
                if normalized:
                    return normalized
    return ""


def read_m3u8_variants(url, headers):
    if not url or ".m3u8" not in url.lower():
        return []
    request_headers = parse_header_block(headers)
    if "User-Agent" not in request_headers:
        request_headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36"
    try:
        request = Request(url, headers=request_headers)
        with urlopen(request, timeout=10) as response:
            text = response.read(1024 * 512).decode("utf-8", errors="ignore")
    except Exception:
        return []
    variants = []
    pending = None
    for raw_line in text.splitlines():
        line = raw_line.strip()
        if line.startswith("#EXT-X-STREAM-INF:"):
            attrs = {}
            for key, value in re.findall(r"([A-Z0-9-]+)=((?:\"[^\"]+\")|[^,]+)", line):
                attrs[key] = value.strip('"')
            pending = attrs
            continue
        if pending is not None and line and not line.startswith("#"):
            item_url = urljoin(url, line)
            variants.append((pending, item_url))
            pending = None
    return variants


def sort_variants(variants):
    return sorted(variants, key=lambda item: int(item[0].get("BANDWIDTH") or 0), reverse=True)


def parse_header_block(headers):
    result = {}
    for item in split_header_block(headers or ""):
        if ":" not in item:
            continue
        key, value = item.split(":", 1)
        key = key.strip()
        value = value.strip()
        if key and value:
            result[key] = value
    return result


def split_header_block(headers):
    parts = []
    current = []
    index = 0
    while index < len(headers):
        ch = headers[index]
        if ch in "\r\n" or is_header_separator(headers, index):
            add_header_part(parts, current)
            if ch == "\r" and index + 1 < len(headers) and headers[index + 1] == "\n":
                index += 1
        else:
            current.append(ch)
        index += 1
    add_header_part(parts, current)
    return parts


def is_header_separator(value, index):
    if value[index] != ";":
        return False
    start = index + 1
    while start < len(value) and value[start].isspace():
        start += 1
    colon = value.find(":", start)
    if colon <= start:
        return False
    next_break = min([pos for pos in (value.find(";", start), value.find("\r", start), value.find("\n", start)) if pos >= 0] or [-1])
    if next_break >= 0 and next_break < colon:
        return False
    return all(ch.isalnum() or ch == "-" for ch in value[start:colon])


def add_header_part(parts, current):
    part = "".join(current).strip()
    if part:
        parts.append(part)
    current.clear()


def read_json_url(url, headers=None, data=None, timeout=12):
    request = Request(url, headers=headers or {}, data=data)
    with urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8", errors="ignore"))


def get_twitch_profile(channel):
    headers = {
        "Client-ID": "kimne78kx3ncx6brgo4mv6wki5h1ko",
        "Content-Type": "application/json",
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36",
        "Referer": "https://www.twitch.tv/",
    }
    body = json.dumps([{
        "operationName": "ChannelShell",
        "variables": {"login": channel},
        "extensions": {
            "persistedQuery": {
                "version": 1,
                "sha256Hash": "580ab410bcd0c1ad194224957ae2241e5d252b2c5173d8e0cce9d32d5bb14efe",
            }
        },
    }]).encode("utf-8")
    try:
        data = read_json_url("https://gql.twitch.tv/gql", headers=headers, data=body)
        user = data[0].get("data", {}).get("userOrError", {}) if isinstance(data, list) and data else {}
    except Exception:
        return {}
    return {
        "avatar_thumb_url": user.get("profileImageURL") or "",
        "uid": user.get("login") or channel,
    }


def target_quality_index(quality):
    return {
        "OD": 0,
        "BD": 1,
        "UHD": 2,
        "HD": 3,
        "SD": 4,
        "LD": 4,
    }.get(normalize_quality(quality), 0)


def enrich_from_m3u8(result):
    if result.get("platform") == "Bilibili":
        return result
    if result.get("platform") == "twitch":
        master_url = result.get("hls_url") or result.get("record_url") or ""
        variants = sort_variants(read_m3u8_variants(master_url, result.get("headers") or ""))
        if variants:
            attrs, _ = variants[min(target_quality_index(result.get("quality")), len(variants) - 1)]
            if not result.get("resolution"):
                resolution = attrs.get("RESOLUTION")
                if resolution:
                    result["resolution"] = resolution.lower()
            if not result.get("bitrate"):
                result["bitrate"] = normalize_bitrate_text(attrs.get("BANDWIDTH"))
        if master_url:
            result["record_url"] = master_url
            result["hls_url"] = master_url
        return result
    record_url = result.get("record_url") or result.get("hls_url") or ""
    headers = result.get("headers") or ""
    variants = sort_variants(read_m3u8_variants(record_url, headers))
    if not variants:
        variants = sort_variants(read_m3u8_variants(result.get("hls_url") or "", headers))
    if not variants:
        return result
    index = min(target_quality_index(result.get("quality")), len(variants) - 1)
    attrs, selected_url = variants[index]
    if selected_url:
        result["record_url"] = selected_url
        result["hls_url"] = selected_url
    if not result.get("resolution"):
        resolution = attrs.get("RESOLUTION")
        if resolution:
            result["resolution"] = resolution.lower()
    if not result.get("bitrate"):
        result["bitrate"] = normalize_bitrate_text(attrs.get("BANDWIDTH"))
    return result


def has_stream(result):
    if not isinstance(result, dict):
        return False
    return bool(result.get("record_url") or result.get("hls_url") or result.get("flv_url"))


def merge_stream_result(primary, fallback):
    if not isinstance(primary, dict) or not isinstance(fallback, dict):
        return primary
    for key in ("record_url", "hls_url", "flv_url", "resolution", "bitrate", "headers"):
        if not primary.get(key) and fallback.get(key):
            primary[key] = fallback[key]
    return primary


def direct_result(url, quality):
    record_url = url
    is_hls = ".m3u8" in url.lower()
    is_flv = ".flv" in url.lower()
    name = f"custom_{uuid.uuid4().hex[:8]}"
    return {
        "room_url": url,
        "is_live_streaming": True,
        "nickname": name,
        "avatar_thumb_url": "",
        "flv_url": record_url if is_flv else "",
        "hls_url": record_url if is_hls else "",
        "record_url": record_url,
        "platform": "Custom",
        "title": "",
        "quality": display_quality(quality),
        "uid": last_path(url),
        "resolution": find_resolution({"record_url": record_url}, [record_url]),
        "bitrate": find_bitrate({"record_url": record_url}, [record_url]),
        "headers": "",
        "source": "direct",
    }


def select_record_url(url, info):
    flv_url = info.get("flv_url") or ""
    record_url = info.get("record_url") or info.get("m3u8_url") or info.get("hls_url") or flv_url or ""
    if ("douyin" in url or "tiktok" in url) and flv_url:
        codec = parse_qs(urlparse(flv_url).query).get("codec", [])
        if not codec or codec[0].lower() != "h265":
            return flv_url
    return record_url


def first_url(value):
    if isinstance(value, str):
        return value if value.startswith(("http://", "https://")) else ""
    if isinstance(value, dict):
        for key in ("url_list", "urlList", "urls"):
            found = first_url(value.get(key))
            if found:
                return found
        for key in ("uri", "url", "avatar", "avatar_thumb", "avatarThumb", "web_uri"):
            found = first_url(value.get(key))
            if found:
                return found
    if isinstance(value, (list, tuple)):
        for item in value:
            found = first_url(item)
            if found:
                return found
    return ""


def find_avatar(value):
    if not isinstance(value, dict):
        return ""

    keys = (
        "avatar_thumb_url",
        "avatarThumb",
        "avatarLarger",
        "avatarMedium",
        "avatar_thumb",
        "avatar_large",
        "avatar_medium",
        "avatar_url",
        "avatar",
        "portrait",
        "face",
        "headUrl",
    )

    for key in keys:
        found = first_url(value.get(key))
        if found:
            return found

    user_keys = (
        "user",
        "owner",
        "author",
        "anchor",
        "profile",
        "profileInfo",
        "userInfo",
        "streamer",
        "streamerInfo",
        "simpleUserInfo",
    )

    for key in user_keys:
        found = find_avatar(value.get(key))
        if found:
            return found
    return ""


def merge_avatar(info, data):
    if isinstance(info, dict) and not (info.get("avatar") or info.get("avatar_thumb_url")):
        avatar = find_avatar(data)
        if avatar:
            info["avatar_thumb_url"] = avatar
    return info


async def get_bilibili_stream_url_precise(spider, json_data, quality, cookies, proxy):
    if not json_data.get("live_status"):
        return {
            "anchor_name": json_data.get("anchor_name", ""),
            "is_live": False,
            "avatar_thumb_url": find_avatar(json_data),
        }
    room_url = json_data.get("room_url", "")
    quality_map = {
        "OD": "10000",
        "BD": "400",
        "UHD": "250",
        "HD": "150",
        "SD": "80",
        "LD": "80",
    }
    qn = quality_map.get(normalize_quality(quality), "10000")
    try:
        play_url, current_qn = await get_bilibili_hls_from_play_info(room_url, qn, cookies, proxy)
    except Exception:
        play_url = ""
        current_qn = ""
    if not play_url:
        import src.stream as dlr_stream
        return await dlr_stream.get_bilibili_stream_url(json_data, video_quality=quality, cookies=cookies, proxy_addr=proxy)
    return {
        "anchor_name": json_data.get("anchor_name", ""),
        "is_live": True,
        "title": json_data.get("title", ""),
        "quality": normalize_quality(quality),
        "m3u8_url": play_url,
        "record_url": play_url,
        "avatar_thumb_url": find_avatar(json_data),
        "uid": str(json_data.get("uid") or last_path(room_url)),
    }


async def get_bilibili_hls_from_play_info(room_url, qn, cookies, proxy):
    from src.http_clients.async_http import async_req
    room_id = last_path(room_url.split("?", 1)[0])
    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36",
        "Accept-Language": "zh-CN,zh;q=0.9,en;q=0.8",
        "Origin": "https://live.bilibili.com",
        "Referer": room_url,
    }
    if cookies:
        headers["Cookie"] = cookies
    params = {
        "room_id": room_id,
        "protocol": "0,1",
        "format": "0,1,2",
        "codec": "0,1,2",
        "qn": qn,
        "platform": "web",
        "ptype": "8",
        "dolby": "5",
        "panorama": "1",
        "hdr_type": "0,1",
    }
    query = "&".join(f"{key}={value}" for key, value in params.items())
    text = await async_req(f"https://api.live.bilibili.com/xlive/web-room/v2/index/getRoomPlayInfo?{query}", proxy_addr=proxy, headers=headers)
    data = json.loads(text)
    playurl = data.get("data", {}).get("playurl_info", {}).get("playurl", {})
    streams = playurl.get("stream") or []
    candidates = []
    for stream_item in streams:
        for format_item in stream_item.get("format", []):
            format_name = format_item.get("format_name", "")
            for codec_item in format_item.get("codec", []):
                base_url = codec_item.get("base_url") or ""
                url_info = codec_item.get("url_info") or []
                if not base_url or not url_info:
                    continue
                score = score_bilibili_candidate(format_name, codec_item, qn)
                for host_item in url_info:
                    url = (host_item.get("host") or "") + base_url + (host_item.get("extra") or "")
                    candidates.append((score, int(codec_item.get("current_qn") or 0), url))
    if not candidates:
        return "", ""
    candidates.sort(key=lambda item: item[0], reverse=True)
    _, current_qn, url = candidates[0]
    return url, str(current_qn)


def score_bilibili_candidate(format_name, codec_item, target_qn):
    current_qn = int(codec_item.get("current_qn") or 0)
    target = int(target_qn or 0)
    codec_name = str(codec_item.get("codec_name") or "").lower()
    score = current_qn
    if current_qn <= target:
        score += 1_000_000
    score -= abs(target - current_qn)
    if format_name in ("ts", "fmp4"):
        score += 50_000
    if "avc" in codec_name:
        score += 10_000
    return score


async def get_douyu_info_from_betard(url, proxy, cookies):
    from src.http_clients.async_http import async_req
    rid = douyu_room_id(url)
    headers = {
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36",
        "Referer": f"https://www.douyu.com/{rid}",
    }
    if cookies:
        headers["Cookie"] = cookies
    text = await async_req(f"https://www.douyu.com/betard/{rid}", proxy_addr=proxy, headers=headers)
    data = json.loads(text)
    room = data.get("room") or {}
    avatar = room.get("owner_avatar") or room.get("avatar_small") or ""
    if not avatar and isinstance(room.get("avatar"), dict):
        avatar = room["avatar"].get("big") or room["avatar"].get("middle") or room["avatar"].get("small") or ""
    return {
        "anchor_name": room.get("nickname") or room.get("owner_name") or "",
        "is_live": room.get("videoLoop") == 0 and room.get("show_status") == 1,
        "title": str(room.get("room_name") or "").replace("&nbsp;", ""),
        "room_id": room.get("room_id") or rid,
        "avatar": avatar,
        "avatar_thumb_url": avatar,
        "uid": str(room.get("owner_uid") or rid),
    }


async def get_douyu_preview_stream(url, proxy, cookies):
    from src.http_clients.async_http import async_req
    rid = douyu_room_id(url)
    did = "10000000000000000000000000001501"
    t13 = str(int(time.time() * 1000))
    auth = hashlib.md5((rid + t13).encode("utf-8")).hexdigest()
    headers = {
        "rid": rid,
        "time": t13,
        "auth": auth,
        "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Safari/537.36",
        "Referer": f"https://www.douyu.com/{rid}",
    }
    if cookies:
        headers["Cookie"] = cookies
    text = await async_req(
        f"https://playweb.douyucdn.cn/lapi/live/hlsH5Preview/{rid}",
        proxy_addr=proxy,
        headers=headers,
        data={"rid": rid, "did": did},
    )
    data = json.loads(text)
    stream_data = data.get("data") or {}
    rtmp_url = stream_data.get("rtmp_url") or ""
    rtmp_live = stream_data.get("rtmp_live") or ""
    if not rtmp_url or not rtmp_live:
        return ""
    return f"{rtmp_url.rstrip('/')}/{rtmp_live}"


def normalize_info(url, platform, info, quality, source):
    if not isinstance(info, dict):
        return None
    record_url = select_record_url(url, info)
    flv_url = info.get("flv_url") or ""
    hls_url = info.get("m3u8_url") or info.get("hls_url") or ""
    if record_url and not flv_url and ".flv" in record_url.lower():
        flv_url = record_url
    if record_url and not hls_url and ".m3u8" in record_url.lower():
        hls_url = record_url
    headers = RECORD_HEADERS.get(platform, "")
    if platform == "Shopee":
        parsed = urlparse(url)
        headers = f"origin:{parsed.scheme}://{parsed.netloc}"
    urls = [record_url, flv_url, hls_url]
    result = {
        "room_url": url,
        "is_live_streaming": info.get("is_live"),
        "nickname": info.get("anchor_name") or info.get("nickname") or "",
        "avatar_thumb_url": info.get("avatar") or info.get("avatar_thumb_url") or "",
        "flv_url": flv_url,
        "hls_url": hls_url,
        "record_url": record_url,
        "platform": platform,
        "title": info.get("title") or "",
        "quality": display_quality(info.get("quality") or quality),
        "uid": find_uid(url, info),
        "resolution": find_resolution(info, urls),
        "bitrate": find_bitrate(info, urls),
        "headers": headers,
        "source": source,
    }
    if platform == "twitch" and not result["avatar_thumb_url"]:
        result.update({key: value for key, value in get_twitch_profile(last_path(url)).items() if value})
    return enrich_from_m3u8(result)


def flatten_url(value):
    if isinstance(value, str):
        return value if value.startswith(("http://", "https://", "rtmp://")) else ""
    if isinstance(value, dict):
        for item in value.values():
            found = flatten_url(item)
            if found:
                return found
    if isinstance(value, (list, tuple, set)):
        for item in value:
            found = flatten_url(item)
            if found:
                return found
    return ""


async def resolve_dlr(url, quality, proxy, args, config):
    if str(DLR_ROOT) not in sys.path:
        sys.path.insert(0, str(DLR_ROOT))
    from src import spider, stream

    domestic_cookie = args.cookie_china or ""
    oversea_cookie = args.cookie_oversea or ""
    info = None
    platform = ""

    if is_match(url, "douyin.com/"):
        platform = "Douyin"
        current_cookie = cookie(config, "douyin", domestic_cookie)
        if "v.douyin.com" not in url and "/user/" not in url and not get_nested(config, "options", "prefer_douyin_app", default=False):
            data = await spider.get_douyin_web_stream_data(url=url, proxy_addr=proxy, cookies=current_cookie)
        else:
            data = await spider.get_douyin_app_stream_data(url=url, proxy_addr=proxy, cookies=current_cookie)
        info = merge_avatar(await stream.get_douyin_stream_url(data, quality, proxy), data)
    elif is_match(url, "https://www.tiktok.com/"):
        platform = "TikTok"
        data = await spider.get_tiktok_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "tiktok", oversea_cookie))
        info = merge_avatar(await stream.get_tiktok_stream_url(data, quality, proxy), data)
    elif is_match(url, "https://live.kuaishou.com/"):
        platform = "Kuaishou"
        data = await spider.get_kuaishou_stream_data2(url=url, proxy_addr=proxy, cookies=cookie(config, "kuaishou", domestic_cookie))
        info = merge_avatar(await stream.get_kuaishou_stream_url(data, quality), data)
    elif is_match(url, "https://www.huya.com/"):
        platform = "Huya"
        current_cookie = cookie(config, "huya", domestic_cookie)
        web_data = {}
        try:
            web_data = await spider.get_huya_stream_data(url=url, proxy_addr=proxy, cookies=current_cookie)
        except Exception:
            web_data = {}
        if quality not in ("OD", "BD", "UHD"):
            info = merge_avatar(await stream.get_huya_stream_url(web_data, quality), web_data)
        else:
            info = merge_avatar(await spider.get_huya_app_stream_url(url=url, proxy_addr=proxy, cookies=current_cookie), web_data)
    elif host_contains(url, "douyu.com"):
        platform = "Douyu"
        url = normalize_douyu_url(url)
        current_cookie = cookie(config, "douyu", domestic_cookie)
        try:
            data = await spider.get_douyu_info_data(url=url, proxy_addr=proxy, cookies=current_cookie)
        except Exception:
            data = {}
        betard_data = await get_douyu_info_from_betard(url, proxy, current_cookie)
        if not isinstance(data, dict) or not data.get("anchor_name"):
            data = betard_data
        else:
            data = {**betard_data, **data}
            if betard_data.get("avatar_thumb_url"):
                data["avatar_thumb_url"] = betard_data["avatar_thumb_url"]
                data["avatar"] = betard_data["avatar_thumb_url"]
            if betard_data.get("uid"):
                data["uid"] = betard_data["uid"]
        try:
            info = await stream.get_douyu_stream_url(data, video_quality=quality, cookies=current_cookie, proxy_addr=proxy)
        except Exception:
            info = data
        if not isinstance(info, dict):
            info = data
        if data.get("avatar_thumb_url") and not (info.get("avatar_thumb_url") or info.get("avatar")):
            info["avatar_thumb_url"] = data["avatar_thumb_url"]
        if not has_stream(info):
            preview_url = await get_douyu_preview_stream(url, proxy, current_cookie)
            if preview_url:
                info["hls_url"] = preview_url
                info["record_url"] = preview_url
    elif is_match(url, "https://www.yy.com/"):
        platform = "YY"
        data = await spider.get_yy_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "yy", domestic_cookie))
        info = await stream.get_yy_stream_url(data)
    elif is_match(url, "https://live.bilibili.com/"):
        platform = "Bilibili"
        current_cookie = cookie(config, "bilibili", domestic_cookie)
        data = await spider.get_bilibili_room_info(url=url, proxy_addr=proxy, cookies=current_cookie)
        info = merge_avatar(await get_bilibili_stream_url_precise(spider, data, quality, current_cookie, proxy), data)
    elif is_match(url, "http://xhslink.com/", "https://xhslink.com/", "https://www.xiaohongshu.com/"):
        platform = "Xiaohongshu"
        info = await spider.get_xhs_stream_url(url, proxy_addr=proxy, cookies=cookie(config, "xhs", domestic_cookie))
    elif is_match(url, "www.bigo.tv/", "slink.bigovideo.tv/"):
        platform = "Bigo"
        info = await spider.get_bigo_stream_url(url, proxy_addr=proxy, cookies=cookie(config, "bigo", oversea_cookie))
    elif is_match(url, "https://app.blued.cn/"):
        platform = "Blued"
        info = await spider.get_blued_stream_url(url, proxy_addr=proxy, cookies=cookie(config, "blued", domestic_cookie))
    elif is_match(url, "sooplive.co.kr/", "sooplive.com/"):
        platform = "SOOP"
        data = await spider.get_sooplive_stream_data(
            url=url,
            proxy_addr=proxy,
            cookies=cookie(config, "sooplive", oversea_cookie),
            username=account(config, "sooplive", "username"),
            password=account(config, "sooplive", "password"),
        )
        info = await stream.get_stream_url(data, quality, spec=True)
    elif is_match(url, "cc.163.com/"):
        platform = "NetEaseCC"
        data = await spider.get_netease_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "netease", domestic_cookie))
        info = await stream.get_netease_stream_url(data, quality)
    elif is_match(url, "qiandurebo.com/"):
        platform = "Qiandurebo"
        info = await spider.get_qiandurebo_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "qiandurebo", domestic_cookie))
    elif is_match(url, "www.pandalive.co.kr/"):
        platform = "PandaTV"
        data = await spider.get_pandatv_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "pandatv", oversea_cookie))
        info = await stream.get_stream_url(data, quality, spec=True)
    elif is_match(url, "fm.missevan.com/"):
        platform = "MaoerFM"
        info = await spider.get_maoerfm_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "maoerfm", domestic_cookie))
    elif is_match(url, "www.winktv.co.kr/"):
        platform = "WinkTV"
        data = await spider.get_winktv_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "winktv", oversea_cookie))
        info = await stream.get_stream_url(data, quality, spec=True)
    elif is_match(url, "www.flextv.co.kr/", "www.ttinglive.com/"):
        platform = "FlexTV"
        data = await spider.get_flextv_stream_data(
            url=url,
            proxy_addr=proxy,
            cookies=cookie(config, "flextv", oversea_cookie),
            username=account(config, "flextv", "username"),
            password=account(config, "flextv", "password"),
        )
        info = await stream.get_stream_url(data, quality, spec=True) if isinstance(data, dict) and "play_url_list" in data else data
    elif is_match(url, "look.163.com/"):
        platform = "Look"
        info = await spider.get_looklive_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "look", domestic_cookie))
    elif is_match(url, "www.popkontv.com/"):
        platform = "PopkonTV"
        info = await spider.get_popkontv_stream_url(
            url=url,
            proxy_addr=proxy,
            access_token=token(config, "popkontv_access_token"),
            username=account(config, "popkontv", "username"),
            password=account(config, "popkontv", "password"),
            partner_code=account(config, "popkontv", "partner_code") or "P-00001",
        )
    elif is_match(url, "twitcasting.tv/"):
        platform = "TwitCasting"
        data = await spider.get_twitcasting_stream_url(
            url=url,
            proxy_addr=proxy,
            cookies=cookie(config, "twitcasting", oversea_cookie),
            account_type=account(config, "twitcasting", "account_type") or "normal",
            username=account(config, "twitcasting", "username"),
            password=account(config, "twitcasting", "password"),
        )
        info = await stream.get_stream_url(data, quality, spec=False)
    elif is_match(url, "live.baidu.com/"):
        platform = "Baidu"
        data = await spider.get_baidu_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "baidu", domestic_cookie))
        info = await stream.get_stream_url(data, quality)
    elif is_match(url, "weibo.com/"):
        platform = "Weibo"
        data = await spider.get_weibo_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "weibo", domestic_cookie))
        info = await stream.get_stream_url(data, quality, hls_extra_key="m3u8_url")
    elif is_match(url, "kugou.com/"):
        platform = "Kugou"
        info = await spider.get_kugou_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "kugou", domestic_cookie))
    elif host_contains(url, "twitch.tv"):
        platform = "twitch"
        url = normalize_twitch_url(url)
        data = await spider.get_twitchtv_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "twitch", oversea_cookie))
        info = merge_avatar(await stream.get_stream_url(data, quality, spec=True), data)
    elif is_match(url, "www.liveme.com/"):
        platform = "LiveMe"
        info = await spider.get_liveme_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "liveme", oversea_cookie))
    elif is_match(url, "www.huajiao.com/"):
        platform = "Huajiao"
        info = await spider.get_huajiao_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "huajiao", domestic_cookie))
    elif is_match(url, "7u66.com/"):
        platform = "Liuxing"
        info = await spider.get_liuxing_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "liuxing", domestic_cookie))
    elif is_match(url, "showroom-live.com/"):
        platform = "ShowRoom"
        data = await spider.get_showroom_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "showroom", oversea_cookie))
        info = await stream.get_stream_url(data, quality, spec=True)
    elif is_match(url, "live.acfun.cn/", "m.acfun.cn/"):
        platform = "Acfun"
        data = await spider.get_acfun_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "acfun", domestic_cookie))
        info = await stream.get_stream_url(data, quality, url_type="flv", flv_extra_key="url")
    elif is_match(url, "live.tlclw.com/"):
        platform = "Changliao"
        info = await spider.get_changliao_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "changliao", domestic_cookie))
    elif is_match(url, "ybw1666.com/"):
        platform = "Yinbo"
        info = await spider.get_yinbo_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "yinbo", domestic_cookie))
    elif is_match(url, "www.inke.cn/"):
        platform = "Yingke"
        info = await spider.get_yingke_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "yingke", domestic_cookie))
    elif is_match(url, "www.zhihu.com/"):
        platform = "Zhihu"
        info = await spider.get_zhihu_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "zhihu", domestic_cookie))
    elif is_match(url, "chzzk.naver.com/"):
        platform = "CHZZK"
        data = await spider.get_chzzk_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "chzzk", oversea_cookie))
        info = await stream.get_stream_url(data, quality, spec=True)
    elif is_match(url, "www.haixiutv.com/"):
        platform = "Haixiu"
        info = await spider.get_haixiu_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "haixiu", domestic_cookie))
    elif is_match(url, "vvxqiu.com/"):
        platform = "VVXqiu"
        info = await spider.get_vvxqiu_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "vvxqiu", domestic_cookie))
    elif is_match(url, "17.live/"):
        platform = "17Live"
        info = await spider.get_17live_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "17live", oversea_cookie))
    elif is_match(url, "www.lang.live/"):
        platform = "LangLive"
        info = await spider.get_langlive_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "langlive", oversea_cookie))
    elif is_match(url, "m.pp.weimipopo.com/"):
        platform = "PPLive"
        info = await spider.get_pplive_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "pplive", domestic_cookie))
    elif is_match(url, ".6.cn/"):
        platform = "SixRoom"
        info = await spider.get_6room_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "6room", domestic_cookie))
    elif is_match(url, "lehaitv.com/"):
        platform = "Lehai"
        info = await spider.get_haixiu_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "lehai", domestic_cookie))
    elif is_match(url, "h.catshow168.com/"):
        platform = "Huamao"
        info = await spider.get_pplive_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "huamao", domestic_cookie))
    elif is_match(url, "live.shopee", "shp.ee/"):
        platform = "Shopee"
        info = await spider.get_shopee_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "shopee", oversea_cookie))
    elif is_match(url, "www.youtube.com/", "youtu.be/"):
        platform = "Youtube"
        data = await spider.get_youtube_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "youtube", oversea_cookie))
        info = await stream.get_stream_url(data, quality, spec=True)
    elif is_match(url, "tb.cn", "huodong.m.taobao.com"):
        platform = "Taobao"
        data = await spider.get_taobao_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "taobao", domestic_cookie))
        info = await stream.get_stream_url(data, quality, url_type="all", hls_extra_key="hlsUrl", flv_extra_key="flvUrl")
    elif is_match(url, "3.cn", "m.jd.com", "eco.m.jd.com"):
        platform = "JD"
        info = await spider.get_jd_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "jd", domestic_cookie))
    elif is_match(url, "faceit.com/"):
        platform = "Faceit"
        data = await spider.get_faceit_stream_data(url=url, proxy_addr=proxy, cookies=cookie(config, "faceit", oversea_cookie))
        info = await stream.get_stream_url(data, quality, spec=True)
    elif is_match(url, "www.miguvideo.com", "m.miguvideo.com"):
        platform = "Migu"
        info = await spider.get_migu_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "migu", domestic_cookie))
    elif is_match(url, "show.lailianjie.com"):
        platform = "Lianjie"
        info = await spider.get_lianjie_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "lianjie", domestic_cookie))
    elif is_match(url, "www.imkktv.com"):
        platform = "Laixiu"
        info = await spider.get_laixiu_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "laixiu", domestic_cookie))
    elif is_match(url, "www.picarto.tv"):
        platform = "Picarto"
        info = await spider.get_picarto_stream_url(url=url, proxy_addr=proxy, cookies=cookie(config, "picarto", oversea_cookie))

    if platform and info:
        return normalize_info(url, platform, info, quality, "douyin_live_recorder")
    return None


async def resolve(args):
    url = normalize_url(args.url)
    quality = normalize_quality(args.quality)
    proxy = args.proxy or None
    config = load_config(args.config)
    if not url:
        return {"room_url": "", "is_live_streaming": None, "nickname": "", "error": "empty url"}
    if ".m3u8" in url.lower() or ".flv" in url.lower():
        return direct_result(url, quality)

    errors = []
    try:
        result = await resolve_dlr(url, quality, proxy, args, config)
        if result:
            return result
    except Exception as exc:
        errors.append(f"douyin_live_recorder: {type(exc).__name__}: {exc}")

    return {
        "room_url": url,
        "is_live_streaming": None,
        "nickname": "",
        "avatar_thumb_url": "",
        "flv_url": "",
        "hls_url": "",
        "record_url": "",
        "platform": "",
        "title": "",
        "quality": display_quality(quality),
        "uid": last_path(url),
        "resolution": "",
        "bitrate": "",
        "headers": "",
        "source": "",
        "error": " | ".join(errors) if errors else "unsupported url or no stream data",
    }


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", required=True)
    parser.add_argument("--quality", default=os.environ.get("STREAM_RESOLVER_QUALITY", "OD"))
    parser.add_argument("--proxy", default="")
    parser.add_argument("--cookie-china", default="")
    parser.add_argument("--cookie-oversea", default="")
    parser.add_argument("--config", default="")
    return parser.parse_args()


def main():
    args = parse_args()
    logs = io.StringIO()
    with contextlib.redirect_stdout(logs):
        result = asyncio.run(resolve(args))
    captured = logs.getvalue()
    if captured:
        sys.stderr.write(captured)
    print(json.dumps(result, ensure_ascii=False, separators=(",", ":")))


if __name__ == "__main__":
    main()
