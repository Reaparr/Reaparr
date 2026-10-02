import { getFontEmbedCSS, toCanvas } from 'html-to-image';

/** Rasterize the frozen, artwork-only export surface without viewport clipping or automatic downscaling. */
export async function renderServerStatsPng(element: HTMLElement): Promise<Blob> {
	await document.fonts.ready;
	if (!element.isConnected) {
		throw new Error('Server stats artwork was removed before export');
	}
	const width = Math.ceil(element.getBoundingClientRect().width);
	const height = Math.ceil(element.getBoundingClientRect().height);
	if (width !== 1200 || height < 1
		|| element.scrollWidth > element.clientWidth + 1
		|| element.scrollHeight > element.clientHeight + 1) {
		throw new Error('Server stats export surface has invalid or overflowing dimensions');
	}
	const options = { width, height, pixelRatio: 1, skipAutoScale: true, preferredFontFormat: 'woff2' };
	const fontEmbedCSS = await getFontEmbedCSS(element, options);
	const canvas = await toCanvas(element, { ...options, fontEmbedCSS });
	if (canvas.width !== width || canvas.height !== height) {
		throw new Error('Browser resized the complete server stats image');
	}
	const context = canvas.getContext('2d');
	if (!context || context.getImageData(Math.floor(width / 2), height - 2, 1, 1).data[3] !== 255) {
		throw new Error('Browser could not render the complete server stats image');
	}
	return new Promise<Blob>((resolve, reject) => {
		canvas.toBlob((image) => {
			if (image && image.size > 0 && image.type === 'image/png') {
				resolve(image);
			} else {
				reject(new Error('Browser could not encode the server stats as PNG'));
			}
		}, 'image/png');
	});
}
