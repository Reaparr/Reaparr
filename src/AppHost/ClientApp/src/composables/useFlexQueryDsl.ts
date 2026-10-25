import { SortDirection, SortType } from '@dto';
import type { IMediaOverviewSort } from './event-bus';

export const getSortOrder = (direction: SortDirection): 'asc' | 'desc' =>
	direction === SortDirection.NoSort ? SortDirection.Asc : direction;

export const createDefaultMediaOverviewSort = (): IMediaOverviewSort => ({
	field: SortType.Title,
	sort: SortDirection.Asc,
});

export const parseFlexSortDsl = (
	value: string | undefined,
	supportedFields: readonly SortType[] = Object.values(SortType),
): IMediaOverviewSort | null => {
	if (value === undefined) return createDefaultMediaOverviewSort();
	if (!value) return null;

	const parts = value.split(':');
	if (parts.length !== 2) return null;

	const [fieldValue, directionValue] = parts;
	const field = supportedFields.find((candidate) => candidate === fieldValue);
	if (!field) return null;
	if (directionValue === SortDirection.Asc || directionValue === SortDirection.Desc) {
		return { field, sort: directionValue };
	}
	return null;
};

export const toggleMediaOverviewSort = (current: IMediaOverviewSort, field: SortType): IMediaOverviewSort => ({
	field,
	sort: current.field === field && current.sort === SortDirection.Asc
		? SortDirection.Desc
		: SortDirection.Asc,
});

export const serializeFlexSortDsl = (sort: IMediaOverviewSort): string =>
	`${sort.field}:${getSortOrder(sort.sort)}`;
