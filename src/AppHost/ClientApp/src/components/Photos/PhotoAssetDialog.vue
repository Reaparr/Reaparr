<template>
	<QDialog
		:model-value="photoStore.dialog.open"
		maximized
		transition-show="fade"
		transition-hide="fade"
		@update:model-value="onDialogModelValue"
		@hide="onDialogHide">
		<QCard
			class="photo-asset-dialog bg-dark text-white"
			role="dialog"
			aria-modal="true"
			:aria-label="activeAsset?.title ?? t('components.photo-browser.asset-viewer')"
			data-cy="photo-asset-dialog">
			<div
				v-if="photoStore.slides.length > 0"
				ref="carouselSurface"
				class="photo-asset-dialog__surface"
				:aria-busy="photoStore.navigating">
				<QCarousel
					v-model="carouselModel"
					class="photo-asset-dialog__carousel"
					dark
					animated
					:swipeable="!photoStore.zoomed"
					:infinite="false"
					:navigation="false"
					:arrows="false"
					@update:model-value="onCarouselModelValue">
					<QCarouselSlide
						v-for="slide in photoStore.slides"
						:key="slideKey(slide)"
						:name="slideKey(slide)"
						class="photo-asset-dialog__slide">
						<div
							class="photo-asset-dialog__image-frame"
							:class="{ 'photo-asset-dialog__image-frame--zoomed': photoStore.zoomed && !isClip(slide) }">
							<div
								v-if="slide.loading && !slide.url"
								class="photo-asset-dialog__image-state"
								role="status">
								<QSpinner
									size="48px"
									:aria-label="t('components.photo-browser.image-loading')" />
							</div>
							<img
								v-else-if="slide.url && !slide.error"
								:src="slide.url"
								class="photo-asset-dialog__image"
								:class="{ 'photo-asset-dialog__image--zoomed': photoStore.zoomed && !isClip(slide) }"
								:alt="slide.asset.title"
								:data-cy="slide.index === photoStore.index ? 'photo-viewer-image' : undefined"
								:style="imageStyle(slide)"
								:draggable="false"
								@load="onImageLoaded(slide, $event)"
								@error="onImageError(slide.index)">
							<div
								v-else
								class="photo-asset-dialog__image-state"
								role="alert">
								<QIcon
									name="mdi-image-off-outline"
									size="48px"
									aria-hidden="true" />
								<span>{{ t('components.photo-browser.image-load-error') }}</span>
								<QBtn
									flat
									dense
									:label="t('components.photo-browser.image-retry')"
									data-cy="photo-image-retry"
									@click="retryImage" />
							</div>
						</div>
					</QCarouselSlide>
					<template #control>
						<QCarouselControl
							v-if="photoStore.count > 1"
							position="top"
							class="photo-asset-dialog__counter"
							:offset="[0, 16]">
							<span
								class="photo-asset-dialog__counter-label"
								data-cy="photo-viewer-counter"
								role="status"
								:aria-label="`${photoStore.index + 1} / ${photoStore.count}`">
								{{ photoStore.index + 1 }} / {{ photoStore.count }}
							</span>
						</QCarouselControl>
						<Dock
							v-if="activeAsset"
							position="bottom"
							:model="dockItems"
							class="photo-asset-dialog__dock"
							:aria-label="t('components.photo-browser.asset-viewer')"
							data-cy="photo-viewer-dock"
							:pt="dockPassThrough">
							<template #item="{ item, props }">
								<button
									type="button"
									v-bind="props.action"
									:aria-label="(item as PhotoDockItem).label"
									:aria-pressed="item.dataCy === 'photo-viewer-zoom' ? photoStore.zoomed : undefined"
									@click.stop="activateDockAction($event, item as PhotoDockItem)">
									<span class="photo-asset-dialog__dock-icon">
										<QIcon
											:name="item.icon"
											size="1.25rem"
											aria-hidden="true" />
										<QTooltip>{{ item.label }}</QTooltip>
									</span>
								</button>
							</template>
						</Dock>
						<QCarouselControl
							v-if="photoStore.count > 1"
							position="left"
							class="row items-center photo-asset-dialog__navigation"
							:offset="[16, 0]">
							<QBtn
								flat
								round
								icon="mdi-chevron-left"
								:aria-label="t('components.photo-browser.previous-asset')"
								data-cy="photo-viewer-previous"
								@click="goPrevious" />
						</QCarouselControl>
						<QCarouselControl
							v-if="photoStore.count > 1"
							position="right"
							class="row items-center photo-asset-dialog__navigation"
							:offset="[16, 0]">
							<QBtn
								flat
								round
								icon="mdi-chevron-right"
								:aria-label="t('components.photo-browser.next-asset')"
								data-cy="photo-viewer-next"
								@click="goNext" />
						</QCarouselControl>
					</template>
				</QCarousel>
			</div>
			<div
				v-else-if="photoStore.dialog.loading"
				class="photo-asset-dialog__image-state"
				role="status">
				<QSpinner
					size="48px"
					:aria-label="t('components.photo-browser.image-loading')" />
			</div>
			<div
				v-else-if="photoStore.dialog.error"
				class="photo-asset-dialog__image-state"
				role="alert"
				data-cy="photo-asset-dialog-error">
				<QIcon
					name="mdi-image-off-outline"
					size="48px"
					aria-hidden="true" />
				<span>{{ t('components.photo-browser.details-load-error') }}</span>
				<QBtn
					flat
					:label="t('components.photo-browser.retry')"
					data-cy="photo-inspection-retry"
					@click="retryInspection" />
			</div>
			<QBtn
				v-if="photoStore.slides.length === 0"
				class="photo-asset-dialog__empty-close"
				flat
				round
				dense
				icon="mdi-close"
				:aria-label="t('components.photo-browser.close-asset-viewer')"
				data-cy="photo-asset-dialog-close"
				@click="closeViewer" />
		</QCard>
	</QDialog>

	<QDialog
		:model-value="photoStore.detailsOpen"
		position="right"
		@update:model-value="onDetailsModelValue"
		@hide="photoStore.closeDetails">
		<QCard
			class="photo-asset-dialog__details dialog-container-background text-white"
			role="dialog"
			aria-modal="true"
			:aria-labelledby="detailsTitleId"
			data-cy="photo-viewer-details">
			<QCardSection class="row items-center justify-between">
				<div
					:id="detailsTitleId"
					class="text-h6">
					{{ detailsAsset?.title ?? t('components.photo-browser.details') }}
				</div>
				<QBtn
					flat
					round
					dense
					icon="mdi-close"
					:aria-label="t('components.photo-browser.close-details')"
					data-cy="photo-viewer-details-close"
					@click="photoStore.closeDetails" />
			</QCardSection>
			<QSeparator />
			<QCardSection
				v-if="photoStore.dialog.loading && !detailsAsset"
				class="row justify-center q-pa-xl"
				role="status">
				<QSpinner
					size="48px"
					:aria-label="t('components.photo-browser.details-loading')" />
			</QCardSection>
			<QCardSection
				v-else-if="photoStore.dialog.error && !detailsAsset"
				data-cy="photo-asset-dialog-error"
				role="alert">
				<QAlert type="error">
					{{ t('components.photo-browser.details-load-error') }}
				</QAlert>
				<QBtn
					flat
					:label="t('components.photo-browser.retry')"
					data-cy="photo-inspection-retry"
					@click="retryInspection" />
			</QCardSection>
			<QCardSection
				v-if="detailsAsset"
				class="photo-asset-dialog__details-content">
				<QBanner
					v-if="photoStore.dialog.error"
					class="q-mb-md"
					role="status">
					{{ t('components.photo-browser.details-reload-error') }}
					<template #action>
						<QBtn
							flat
							:label="t('components.photo-browser.retry')"
							data-cy="photo-inspection-retry"
							@click="retryInspection" />
					</template>
				</QBanner>
				<dl class="photo-asset-dialog__metadata">
					<div>
						<dt>{{ t('components.photo-browser.asset-kind') }}</dt>
						<dd>{{ kindLabel(detailsAsset.assetKind) }}</dd>
					</div>
					<div>
						<dt>{{ t('components.photo-browser.server') }}</dt>
						<dd>{{ detailsAsset.plexServerName }}</dd>
					</div>
					<div>
						<dt>{{ t('components.photo-browser.library') }}</dt>
						<dd>{{ detailsAsset.plexLibraryTitle }}</dd>
					</div>
					<div v-if="detailsAsset.sourceRootTitle">
						<dt>{{ t('components.photo-browser.source-root') }}</dt>
						<dd>{{ detailsAsset.sourceRootTitle }}</dd>
					</div>
					<div v-if="detailsAsset.originalFilename">
						<dt>{{ t('components.photo-browser.filename') }}</dt>
						<dd>{{ detailsAsset.originalFilename }}</dd>
					</div>
					<div v-if="detailsAsset.container">
						<dt>{{ t('components.photo-browser.format') }}</dt>
						<dd>{{ detailsAsset.container }}</dd>
					</div>
					<div>
						<dt>{{ t('components.photo-browser.size') }}</dt>
						<dd><QFileSize :size="detailsAsset.size" /></dd>
					</div>
					<div v-if="detailsAsset.width != null || detailsAsset.height != null">
						<dt>{{ t('components.photo-browser.dimensions') }}</dt>
						<dd>{{ t('components.photo-browser.dimensions-value', { width: detailsAsset.width ?? '—', height: detailsAsset.height ?? '—' }) }}</dd>
					</div>
					<div v-if="detailsAsset.duration">
						<dt>{{ t('components.photo-browser.duration') }}</dt>
						<dd><QDuration :value="detailsAsset.duration" /></dd>
					</div>
					<div v-if="detailsAsset.videoCodec">
						<dt>{{ t('components.photo-browser.video-codec') }}</dt>
						<dd>{{ detailsAsset.videoCodec }}</dd>
					</div>
					<div v-if="detailsAsset.audioCodec">
						<dt>{{ t('components.photo-browser.audio-codec') }}</dt>
						<dd>{{ detailsAsset.audioCodec }}</dd>
					</div>
					<div v-if="detailsAsset.relativeFolderPath || detailsAsset.sourceRootTitle">
						<dt>{{ t('components.photo-browser.location') }}</dt>
						<dd>{{ detailsAsset.relativeFolderPath || detailsAsset.sourceRootTitle }}</dd>
					</div>
					<div v-if="detailsAsset.addedAt">
						<dt>{{ t('components.photo-details.added-at') }}</dt>
						<dd>
							<QDateTime
								:text="detailsAsset.addedAt"
								short-date
								time />
						</dd>
					</div>
					<div v-if="detailsAsset.updatedAt">
						<dt>{{ t('components.photo-details.updated-at') }}</dt>
						<dd>
							<QDateTime
								:text="detailsAsset.updatedAt"
								short-date
								time />
						</dd>
					</div>
					<div v-if="detailsAsset.originallyAvailableAt">
						<dt>{{ t('components.photo-details.originally-available') }}</dt>
						<dd>
							<QDateTime
								:text="detailsAsset.originallyAvailableAt"
								short-date />
						</dd>
					</div>
				</dl>
				<p
					v-if="detailsAsset.summary"
					class="q-mt-md">
					{{ detailsAsset.summary }}
				</p>
				<QBreadcrumbs
					v-if="detailsAsset.breadcrumbs?.length"
					class="q-mt-md"
					:aria-label="t('components.photo-browser.breadcrumbs')">
					<QBreadcrumbsEl
						:label="detailsAsset.plexLibraryTitle"
						:to="libraryRoute(detailsAsset)" />
					<QBreadcrumbsEl
						v-for="(breadcrumb, breadcrumbIndex) in detailsAsset.breadcrumbs"
						:key="breadcrumb.id"
						:label="breadcrumb.title"
						:to="breadcrumbIndex < detailsAsset.breadcrumbs.length - 1 ? folderRoute(detailsAsset, breadcrumb.id) : undefined" />
				</QBreadcrumbs>
				<section
					class="q-mt-lg"
					data-cy="photo-asset-dialog-albums">
					<h3 class="text-subtitle1 q-my-none">
						{{ t('components.photo-browser.album-memberships') }}
					</h3>
					<QAlert
						v-if="!detailsAsset.albums?.items?.length"
						type="info"
						role="status"
						class="q-mt-sm">
						{{ t('components.photo-browser.no-album-memberships') }}
					</QAlert>
					<QVirtualScroll
						v-else
						class="photo-asset-dialog__memberships"
						:items="detailsAsset.albums.items"
						:virtual-scroll-item-size="48"
						separator
						@virtual-scroll="onMembershipVirtualScroll">
						<template #default="{ item: album }">
							<QItem
								:key="`${album.plexLibraryId}:${album.id}`"
								clickable
								:to="`/photos/${album.plexLibraryId}/details/${album.id}`">
								<QItemSection>{{ album.title }}</QItemSection>
								<QItemSection side>
									{{ album.plexLibraryTitle }}
								</QItemSection>
							</QItem>
						</template>
					</QVirtualScroll>
					<div
						v-if="get(membershipPageInFlight) !== null"
						class="row justify-center q-pa-sm"
						role="status">
						<QSpinner
							size="24px"
							:aria-label="t('components.photo-browser.memberships-loading')" />
					</div>
				</section>
			</QCardSection>
		</QCard>
	</QDialog>
</template>

<script setup lang="ts">
import { get, set, useEventListener, useSwipe } from '@vueuse/core';
import { useSubscription } from '@vueuse/rxjs';
import { finalize } from 'rxjs/operators';
import { clamp } from 'lodash-es';
import type { DockPassThroughMethodOptions } from 'primevue/dock';
import { PhotoAssetKind, PhotoItemKind, type PhotoAssetBrowseDTO, type PhotoAssetInspectionDTO } from '@dto';
import type { PhotoOverviewItem, PhotoViewerSlide } from '@/store/photoBrowsingStore';
import { useMediaStore, usePhotoBrowsingStore } from '@store';

const photoStore = usePhotoBrowsingStore();
const mediaStore = useMediaStore();
const { t } = useI18n();
const detailsTitleId = 'photo-asset-details-title';
const emit = defineEmits<{
	(e: 'download', item: PhotoOverviewItem): void;
}>();
const carouselModel = ref('');
const carouselSurface = shallowRef<HTMLElement | null>(null);
const panOffset = reactive({ x: 0, y: 0 });
const swipeOrigin = reactive({ x: 0, y: 0 });
const nativeDimensions = reactive(new Map<string, { width: number; height: number }>());
const membershipPageInFlight = ref<number | null>(null);
const zoomScale = shallowRef(1);
const pointerDrag = reactive({
	active: false,
	pointerId: -1,
	startClientX: 0,
	startClientY: 0,
	startPanX: 0,
	startPanY: 0,
});
const pointerCaptureTarget = shallowRef<HTMLElement | null>(null);

const activeSlide = computed(() => photoStore.slides.find((slide) => slide.index === photoStore.index) ?? null);
const activeAsset = computed<PhotoAssetBrowseDTO | null>(() => photoStore.current ?? get(activeSlide)?.asset ?? null);
const detailsAsset = computed<PhotoAssetInspectionDTO | null>(() => photoStore.dialog.asset);
type PhotoDockItem = {
	label: string;
	icon: string;
	dataCy: string;
	action: () => void;
};

const dockPassThrough = {
	itemLink: ({ context }: DockPassThroughMethodOptions) => ({
		'data-cy': context.item.dataCy,
	}),
};

const dockItems = computed<PhotoDockItem[]>(() => {
	const asset = get(activeAsset);
	const items: PhotoDockItem[] = [{
		label: t('components.photo-browser.close-asset-viewer'),
		icon: 'mdi-close',
		dataCy: 'photo-asset-dialog-close',
		action: closeViewer,
	}];

	if (asset && !isClipAsset(asset)) {
		items.push({
			label: photoStore.zoomed ? t('components.photo-browser.zoom-fit') : t('components.photo-browser.zoom-native'),
			icon: photoStore.zoomed ? 'mdi-fit-to-screen-outline' : 'mdi-magnify-plus-outline',
			dataCy: 'photo-viewer-zoom',
			action: toggleZoom,
		});
	}

	if (asset) {
		items.push({
			label: t('general.commands.download'),
			icon: 'mdi-download',
			dataCy: 'photo-viewer-download',
			action: downloadCurrentAsset,
		});
	}

	items.push({
		label: t('components.photo-browser.details'),
		icon: 'mdi-information-outline',
		dataCy: 'photo-viewer-details-action',
		action: openDetails,
	});

	return items;
});

function activateDockAction(event: Event, item: PhotoDockItem): void {
	event.preventDefault();
	item.action();
}

function slideKey(slide: PhotoViewerSlide): string {
	return `${slide.asset.plexLibraryId}:${slide.asset.id}`;
}

function isClip(slide: PhotoViewerSlide): boolean {
	return isClipAsset(slide.asset);
}

function isClipAsset(asset: PhotoAssetBrowseDTO): boolean {
	return asset.assetKind === PhotoAssetKind.VideoClip;
}

function kindLabel(kind: PhotoAssetKind): string {
	return kind === PhotoAssetKind.VideoClip ? t('components.photo-browser.kind-clip') : t('components.photo-browser.kind-image');
}

function imageStyle(slide: PhotoViewerSlide): Record<string, string> {
	if (!photoStore.zoomed || isClip(slide)) return {};
	const dimensions = nativeDimensions.get(slideKey(slide));
	return {
		width: dimensions ? `${dimensions.width * get(zoomScale)}px` : 'auto',
		height: dimensions ? `${dimensions.height * get(zoomScale)}px` : 'auto',
		transform: `translate3d(${panOffset.x}px, ${panOffset.y}px, 0)`,
	};
}

function resetPan(): void {
	panOffset.x = 0;
	panOffset.y = 0;
}
function setPanPosition(x: number, y: number): void {
	const slide = get(activeSlide);
	if (!photoStore.zoomed || !slide || isClip(slide)) return;
	const dimensions = nativeDimensions.get(slideKey(slide));
	const bounds = get(carouselSurface)?.getBoundingClientRect();
	const maxX = dimensions && bounds ? Math.max(0, (dimensions.width * get(zoomScale) - bounds.width) / 2) : Number.POSITIVE_INFINITY;
	const maxY = dimensions && bounds ? Math.max(0, (dimensions.height * get(zoomScale) - bounds.height) / 2) : Number.POSITIVE_INFINITY;
	panOffset.x = Math.max(-maxX, Math.min(maxX, x));
	panOffset.y = Math.max(-maxY, Math.min(maxY, y));
}

function syncCarouselModel(): void {
	const slide = get(activeSlide);
	set(carouselModel, slide ? slideKey(slide) : '');
}

function onImageLoaded(slide: PhotoViewerSlide, event: Event): void {
	const image = event.target;
	if (!(image instanceof HTMLImageElement) || image.naturalWidth <= 0 || image.naturalHeight <= 0) return;
	nativeDimensions.set(slideKey(slide), { width: image.naturalWidth, height: image.naturalHeight });
}

function onCarouselModelValue(value: string | number): void {
	const slide = photoStore.slides.find((candidate) => slideKey(candidate) === String(value));
	if (!slide || slide.index === photoStore.index) return;
	goTo(slide.index);
}

function goTo(index: number): void {
	if (photoStore.count <= 0) return;
	const normalized = ((index % photoStore.count) + photoStore.count) % photoStore.count;
	useSubscription(photoStore.showAsset(normalized).subscribe());
}

function goPrevious(): void {
	goTo(photoStore.index - 1);
}

function goNext(): void {
	goTo(photoStore.index + 1);
}

function downloadCurrentAsset(): void {
	const asset = get(activeAsset);
	if (asset) emit('download', { kind: PhotoItemKind.Asset, asset, index: photoStore.index });
}

function toggleZoom(): void {
	resetPan();
	set(zoomScale, 1);
	photoStore.setZoomed(!photoStore.zoomed);
}

function onWheel(event: WheelEvent): void {
	const slide = get(activeSlide);
	if (!slide || isClip(slide)) return;
	const frame = getWheelFrame(event);
	const dimensions = nativeDimensions.get(slideKey(slide));
	if (!frame || !dimensions) return;
	const fitScale = getFitScale(frame, dimensions);
	const currentScale = photoStore.zoomed ? get(zoomScale) : fitScale;
	const nextScale = clamp(currentScale * (event.deltaY < 0 ? 1.2 : 1 / 1.2), fitScale, 4);
	event.preventDefault();
	endPointerDrag();
	set(zoomScale, nextScale);
	photoStore.setZoomed(nextScale > fitScale);
	setPanPosition(panOffset.x, panOffset.y);
}

function getWheelFrame(event: WheelEvent): HTMLElement | null {
	if (photoStore.detailsOpen || event.deltaY === 0) return null;
	if (!(event.target instanceof HTMLElement)) return null;
	return event.target.closest<HTMLElement>('.photo-asset-dialog__image-frame');
}

function getFitScale(frame: HTMLElement, dimensions: { width: number; height: number }): number {
	const bounds = frame.getBoundingClientRect();
	const style = getComputedStyle(frame);
	const width = bounds.width - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight);
	const height = bounds.height - parseFloat(style.paddingTop) - parseFloat(style.paddingBottom);
	return Math.min(1, width / dimensions.width, height / dimensions.height);
}

function closeViewer(): void {
	endPointerDrag();
	photoStore.closeAssetDialog();
}

function onDialogModelValue(value: boolean): void {
	if (!value) closeViewer();
}

function onDialogHide(): void {
	if (photoStore.dialog.open) photoStore.closeAssetDialog();
}

function openDetails(): void {
	useSubscription(photoStore.openDetails().subscribe());
}

function onDetailsModelValue(value: boolean): void {
	if (!value) photoStore.closeDetails();
}

function retryInspection(): void {
	useSubscription(photoStore.retryInspection().subscribe());
}

function retryImage(): void {
	useSubscription(photoStore.retryImage().subscribe());
}
function onImageError(index: number): void {
	useSubscription(photoStore.imageFailed(index).subscribe());
}
function isPanPointerTarget(event: PointerEvent): boolean {
	const target = event.target;
	return target instanceof HTMLElement && target.closest('.photo-asset-dialog__image-frame') !== null;
}

function onPointerDown(event: PointerEvent): void {
	const slide = get(activeSlide);
	const target = event.currentTarget;
	if (!photoStore.zoomed || !slide || isClip(slide) || event.pointerType === 'touch' || event.button !== 0 || !isPanPointerTarget(event) || !(target instanceof HTMLElement)) return;
	try {
		target.setPointerCapture(event.pointerId);
	} catch {
		return;
	}
	pointerDrag.active = true;
	pointerDrag.pointerId = event.pointerId;
	pointerDrag.startClientX = event.clientX;
	pointerDrag.startClientY = event.clientY;
	pointerDrag.startPanX = panOffset.x;
	pointerDrag.startPanY = panOffset.y;
	set(pointerCaptureTarget, target);
	event.preventDefault();
}

function onPointerMove(event: PointerEvent): void {
	if (!pointerDrag.active || event.pointerId !== pointerDrag.pointerId) return;
	if (event.pointerType === 'mouse' && event.buttons === 0) {
		endPointerDrag(event);
		return;
	}
	setPanPosition(
		pointerDrag.startPanX + event.clientX - pointerDrag.startClientX,
		pointerDrag.startPanY + event.clientY - pointerDrag.startClientY,
	);
	event.preventDefault();
}

function endPointerDrag(event?: PointerEvent): void {
	if (!pointerDrag.active || (event && event.pointerId !== pointerDrag.pointerId)) return;
	const target = get(pointerCaptureTarget);
	if (target?.hasPointerCapture(pointerDrag.pointerId)) target.releasePointerCapture(pointerDrag.pointerId);
	pointerDrag.active = false;
	pointerDrag.pointerId = -1;
	set(pointerCaptureTarget, null);
}

function libraryRoute(asset: PhotoAssetInspectionDTO): string {
	return `/photos/${asset.plexLibraryId}?view=folders`;
}

function folderRoute(asset: PhotoAssetInspectionDTO, folderId: number): string {
	return `/photos/${asset.plexLibraryId}?view=folders&folderId=${folderId}`;
}

function requestNextMembershipPage(): void {
	const albums = get(detailsAsset)?.albums;
	if (!albums || get(membershipPageInFlight) !== null || photoStore.dialog.loading || albums.pageSize <= 0 || albums.page * albums.pageSize >= albums.totalCount) return;
	const nextPage = albums.page + 1;
	set(membershipPageInFlight, nextPage);
	useSubscription(photoStore.requestInspectionMembershipPage(nextPage).pipe(
		finalize(() => set(membershipPageInFlight, null)),
	).subscribe());
}

function onMembershipVirtualScroll(details: { to?: number }): void {
	const albums = get(detailsAsset)?.albums;
	if (!albums || details.to == null || details.to < albums.items.length - 1) return;
	requestNextMembershipPage();
}

function isViewerControlTarget(target: EventTarget | null): boolean {
	if (!(target instanceof HTMLElement)) return false;
	return target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName) || Boolean(target.closest('.p-dock'));
}

function onKeyDown(event: KeyboardEvent): void {
	if (!photoStore.dialog.open) return;
	if (event.key === 'Escape') {
		if (photoStore.detailsOpen) {
			event.preventDefault();
			event.stopPropagation();
			photoStore.closeDetails();
		}
		return;
	}
	if (photoStore.detailsOpen || isViewerControlTarget(event.target)) return;
	if (event.key === 'ArrowLeft') {
		event.preventDefault();
		goPrevious();
	} else if (event.key === 'ArrowRight') {
		event.preventDefault();
		goNext();
	} else {
		onZoomKeyDown(event);
	}
}

function onZoomKeyDown(event: KeyboardEvent): void {
	if (event.key === '+' || event.key === '=') {
		event.preventDefault();
		const asset = get(activeAsset);
		if (asset && !isClipAsset(asset)) {
			set(zoomScale, 1);
			photoStore.setZoomed(true);
			resetPan();
		}
	} else if (event.key === '-' || event.key === '0') {
		event.preventDefault();
		photoStore.setZoomed(false);
		resetPan();
	}
}

const { lengthX, lengthY } = useSwipe(carouselSurface, {
	threshold: 24,
	passive: false,
	onSwipeStart: () => {
		if (!photoStore.zoomed) return;
		const slide = get(activeSlide);
		if (!slide || isClip(slide)) return;
		swipeOrigin.x = panOffset.x;
		swipeOrigin.y = panOffset.y;
	},
	onSwipe: () => {
		if (!photoStore.zoomed) return;
		setPanPosition(swipeOrigin.x + get(lengthX), swipeOrigin.y + get(lengthY));
	},
	onSwipeEnd: (_, direction) => {
		if (photoStore.zoomed || !photoStore.dialog.open) return;
		const slides = photoStore.slides;
		const currentPosition = slides.findIndex((slide) => slide.index === photoStore.index);
		if (direction === 'left' && currentPosition === slides.length - 1) goNext();
		if (direction === 'right' && currentPosition === 0) goPrevious();
	},
});
useEventListener(carouselSurface, 'wheel', onWheel, { passive: false });
useEventListener(carouselSurface, 'pointerdown', onPointerDown, { passive: false });
useEventListener(carouselSurface, 'pointermove', onPointerMove, { passive: false });
useEventListener(carouselSurface, 'pointerup', endPointerDrag);
useEventListener(carouselSurface, 'pointercancel', endPointerDrag);
useEventListener(carouselSurface, 'lostpointercapture', endPointerDrag);

useEventListener(window, 'keydown', onKeyDown, { capture: true });

watchDebounced(
	[() => mediaStore.photoPreviewSize.width, () => mediaStore.photoPreviewSize.height],
	() => useSubscription(photoStore.resizeImages().subscribe()),
	{ debounce: 150 },
);

watch(() => [photoStore.index, photoStore.slides], () => {
	const retainedKeys = new Set(photoStore.slides.map(slideKey));
	for (const key of nativeDimensions.keys()) {
		if (!retainedKeys.has(key)) nativeDimensions.delete(key);
	}
	endPointerDrag();
	syncCarouselModel();
}, { immediate: true, deep: true });

watch(() => photoStore.zoomed, (zoomed) => {
	if (!zoomed) {
		endPointerDrag();
		set(zoomScale, 1);
		resetPan();
	}
});
</script>

<style scoped lang="scss">
@use '@/assets/scss/variables.scss' as *;

.photo-asset-dialog {
	position: relative;
	height: 100%;
	background: var(--q-dark-page, #121212);
	&__navigation {
		pointer-events: none;
		> .q-btn {
			pointer-events: auto;
		}
	}

	&__counter {
		pointer-events: none;
	}

	&__counter-label {
		display: inline-flex;
		align-items: center;
		min-height: 2rem;
		padding: 0.25rem 0.75rem;
		border: 1px solid rgba($primary, 0.55);
		border-radius: 999px;
		background: rgba(13, 13, 13, 0.72);
		backdrop-filter: blur(12px);
		color: var(--q-grey-2, #ddd);
		font-variant-numeric: tabular-nums;
	}

	&__dock {
		z-index: 5;
		padding-inline: max(0.5rem, env(safe-area-inset-left)) max(0.5rem, env(safe-area-inset-right));
		pointer-events: none;

		&.p-dock-bottom {
			bottom: calc(0.75rem + env(safe-area-inset-bottom));
		}

		:deep(.p-dock-list-container) {
			pointer-events: auto;
			padding: 0.35rem;
			border: 1px solid rgba($primary, 0.65);
			border-radius: 999px;
			background: rgba(13, 13, 13, 0.78);
			backdrop-filter: blur(16px);
			box-shadow: $box-shadow;
		}

		:deep(.p-dock-list) {
			gap: 0.25rem;
		}

		:deep(.p-dock-item) {
			padding: 0.2rem;
		}

		:deep(.p-dock-item-link) {
			width: 2.75rem;
			height: 2.75rem;
			border: 1px solid transparent;
			border-radius: 50%;
			background: rgba(0, 0, 0, 0.25);
			color: var(--q-grey-2, #ddd);
			cursor: pointer;
			transition:
				background-color 120ms ease,
				border-color 120ms ease,
				color 120ms ease,
				box-shadow 120ms ease;
		}

		:deep(.p-dock-item-link:hover),
		:deep(.p-dock-item-link:focus-visible),
		:deep(.p-dock-item.p-focus .p-dock-item-link) {
			border-color: rgba($primary, 0.85);
			background: rgba($primary, 0.2);
			box-shadow: 0 0 0 2px rgba($primary, 0.18);
			color: #fff;
		}
	}

	&__dock-icon {
		display: inline-flex;
		align-items: center;
		justify-content: center;
		width: 100%;
		height: 100%;
	}
	&__surface,
	&__carousel,
	&__slide,
	&__image-frame {
		width: 100%;
		height: 100%;
	}

	&__surface {
		position: relative;
		min-height: 100vh;
		background: var(--q-dark-page, #121212);
	}
	&__empty-close {
		position: absolute;
		top: 1rem;
		left: 1rem;
		z-index: 2;
		color: var(--q-grey-2, #ddd);
	}

	&__carousel {
		background: var(--q-dark-page, #121212);
	}

	&__slide {
		display: flex;
		align-items: center;
		justify-content: center;
		padding: 0;
	}

	&__image-frame {
		position: relative;
		display: flex;
		align-items: center;
		justify-content: center;
		overflow: hidden;
		padding: 3.5rem clamp(0.5rem, 4vw, 3.5rem);
		touch-action: pan-y;
	}
	&__image-frame--zoomed {
		touch-action: none;
	}

	&__image {
		display: block;
		max-width: 100%;
		max-height: 100%;
		object-fit: contain;
		user-select: none;
		transition: transform 120ms ease-out;
	}

	&__image--zoomed {
		max-width: none;
		max-height: none;
		cursor: grab;
		transition: none;
	}

	&__image-state {
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		gap: 0.75rem;
		width: 100%;
		height: 100%;
		color: var(--q-grey-4, #aaa);
	}

	&__details {
		width: min(32rem, 100vw);
		max-width: 100vw;
		height: 100%;
	}

	&__details-content {
		overflow-y: auto;
	}

	&__metadata {
		display: grid;
		gap: 0.75rem;
		margin: 0;
	}

	&__metadata > div {
		display: grid;
		grid-template-columns: minmax(7rem, auto) 1fr;
		gap: 0.75rem;
	}

	&__metadata dt {
		font-weight: 600;
		color: var(--q-grey-4, #aaa);
	}

	&__metadata dd {
		margin: 0;
		overflow-wrap: anywhere;
	}

	&__memberships {
		max-height: min(32rem, 50vh);
		overflow-y: auto;
	}
}
</style>
