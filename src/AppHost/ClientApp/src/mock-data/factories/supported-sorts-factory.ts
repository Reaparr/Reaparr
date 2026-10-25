import { PlexMediaType, SortType } from '@dto';

/**
 * Returns the sort fields supported by the requested media family.
 */
const supportedSortsByMediaType: Partial<Record<PlexMediaType, SortType[]>> = {
	[PlexMediaType.Movie]: [
		SortType.Title,
		SortType.Year,
		SortType.AddedAt,
		SortType.UpdatedAt,
		SortType.Duration,
		SortType.MediaSize,
		SortType.VideoQuality,
		SortType.ComparisonState,
	],
	[PlexMediaType.TvShow]: [
		SortType.Title,
		SortType.Year,
		SortType.AddedAt,
		SortType.UpdatedAt,
		SortType.Duration,
		SortType.MediaSize,
		SortType.VideoQuality,
		SortType.ComparisonState,
	],
	[PlexMediaType.MusicArtist]: [
		SortType.Title,
		SortType.AddedAt,
		SortType.UpdatedAt,
		SortType.Duration,
		SortType.MediaSize,
		SortType.AudioQuality,
		SortType.ComparisonState,
	],
	[PlexMediaType.PhotoAlbum]: [
		SortType.Title,
		SortType.AddedAt,
		SortType.UpdatedAt,
		SortType.Duration,
		SortType.MediaSize,
	],
	[PlexMediaType.OtherVideos]: [
		SortType.Title,
		SortType.Year,
		SortType.AddedAt,
		SortType.UpdatedAt,
		SortType.Duration,
		SortType.MediaSize,
		SortType.VideoQuality,
	],
};

export function generateSupportedSorts(mediaType: PlexMediaType): SortType[] {
	return [...(supportedSortsByMediaType[mediaType] ?? [])];
}
