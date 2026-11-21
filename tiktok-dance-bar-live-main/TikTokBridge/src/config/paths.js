const path = require("node:path");
const root = path.resolve(__dirname, "../../..");
const data = path.join(root, "Data");
const mediaSources = path.join(root, "MediaSources");
const resources = path.join(root, "UnityProject", "Assets", "Resources");

module.exports = Object.freeze({
    root,
    data,
    music: path.join(data, "Music"),
    videos: path.join(data, "Videos"),
    backgrounds: path.join(data, "Backgrounds"),
    library: path.join(data, "Assets"),
    public: path.join(root, "TikTokBridge", "public"),
    characterGifs: path.join(mediaSources, "Characters", "Gifs"),
    sourceVideos: path.join(mediaSources, "Videos"),
    special: path.join(mediaSources, "Special"),
    banners: path.join(resources, "Banners"),
    resources,
});
