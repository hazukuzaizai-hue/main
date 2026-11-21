const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const catalogFile = path.join(root, 'Data', 'Assets', 'library.json');
const catalog = JSON.parse(fs.readFileSync(catalogFile, 'utf8'));
const missing = [];
const activeItems = catalog.items.filter(item => !item.deleted);
for (const item of activeItems) {
    let location;
    if (item.id.startsWith('builtin:')) {
        const [, kind, name] = item.id.split(':');
        location = path.join(root, 'UnityProject', 'Assets', 'Resources', kind === 'character' ? 'Characters' : 'Banners', name + (kind === 'character' ? '' : '.png'));
    } else {
        const category = { character: 'characters', wing: 'wings', banner: 'banners', avatarFrame: 'avatar-frames' }[item.kind];
        location = path.join(root, 'Data', 'Assets', category, item.id);
        if (fs.existsSync(location) && !fs.readdirSync(location).some(name => /^frame_\d+\.png$/.test(name))) missing.push(`${item.name}: no animation frames`);
    }
    if (!fs.existsSync(location)) missing.push(`${item.name}: ${location}`);
}
for (const [type, selected] of [['wing', catalog.wing], ['banner', catalog.banner]]) {
    if (selected && !activeItems.some(item => item.id === selected && item.kind === type)) missing.push(`Invalid selected ${type}: ${selected}`);
}
for (const file of ['Data/Backgrounds/default.png', 'Data/Dj/source.gif']) {
    if (!fs.existsSync(path.join(root, file))) missing.push(file);
}
const djFrames = path.join(root, 'Data', 'Dj', 'frames');
if (!fs.existsSync(djFrames) || !fs.readdirSync(djFrames).some(name => name.endsWith('.png'))) missing.push('DJ animation frames missing');
if (missing.length) { console.error(missing.join('\n')); process.exit(1); }
console.log(`Verified ${activeItems.length} assets and selected decorations.`);
