import { describe, expect, test } from 'vitest';
import {
	decodeDownloadScheduleDays,
	encodeDownloadScheduleDays,
	getDownloadScheduleSlots,
	isDownloadScheduleLimit,
	MAX_DOWNLOAD_SCHEDULE_LIMIT,
	type DownloadScheduleRange,
} from '@composables/download-schedule';

describe('Download schedule selection', () => {
	test('Should use an exclusive Until boundary including 24:00', () => {
		// Arrange
		const daytimeRange: DownloadScheduleRange = { days: [0], from: 18, until: 21 };
		const endOfDayRange: DownloadScheduleRange = { days: [0], from: 47, until: 48 };

		// Act
		const daytimeSlots = getDownloadScheduleSlots(daytimeRange);
		const endOfDaySlots = getDownloadScheduleSlots(endOfDayRange);

		// Assert
		expect(daytimeSlots).toEqual([18, 19, 20]);
		expect(endOfDaySlots).toEqual([47]);
	});

	test('Should wrap an overnight Sunday range into Monday with exact boundary slots', () => {
		// Arrange
		const range: DownloadScheduleRange = { days: [6], from: 47, until: 1 };

		// Act
		const slots = getDownloadScheduleSlots(range);

		// Assert
		expect(slots).toEqual([0, 335]);
	});

	test('Should deduplicate slots when a selected day is repeated', () => {
		// Arrange
		const range: DownloadScheduleRange = { days: [0, 0], from: 47, until: 1 };

		// Act
		const slots = getDownloadScheduleSlots(range);

		// Assert
		expect(slots).toEqual([47, 48]);
	});

	test.each<{
		label: string;
		range: DownloadScheduleRange;
	}>([
		{ label: 'From equals Until', range: { days: [0], from: 9, until: 9 } },
		{ label: 'day is below Monday', range: { days: [-1], from: 9, until: 10 } },
		{ label: 'day is after Sunday', range: { days: [7], from: 9, until: 10 } },
		{ label: 'day is fractional', range: { days: [1.5], from: 9, until: 10 } },
		{ label: 'From is below midnight', range: { days: [0], from: -1, until: 10 } },
		{ label: 'From is 24:00', range: { days: [0], from: 48, until: 10 } },
		{ label: 'Until is below midnight', range: { days: [0], from: 9, until: -1 } },
		{ label: 'Until is after 24:00', range: { days: [0], from: 9, until: 49 } },
		{ label: 'slot is fractional', range: { days: [0], from: 9.5, until: 10 } },
	])('Should reject a range when $label', ({ range }) => {
		// Act
		const slots = getDownloadScheduleSlots(range);

		// Assert
		expect(slots).toEqual([]);
	});

	test.each([null, 1, MAX_DOWNLOAD_SCHEDULE_LIMIT])(
		'Should accept %s as an Unlimited or positive integer cap boundary',
		(value) => {
			// Act
			const isValid = isDownloadScheduleLimit(value);

			// Assert
			expect(isValid).toBe(true);
		},
	);

	test.each([0, -1, 1.5, MAX_DOWNLOAD_SCHEDULE_LIMIT + 1])(
		'Should reject %s outside the positive Int32 cap boundaries',
		(value) => {
			// Act
			const isValid = isDownloadScheduleLimit(value);

			// Assert
			expect(isValid).toBe(false);
		},
	);
	test('Should apply unsorted daily points and Unlimited resets without carrying Sunday into Monday', () => {
		// Arrange
		const days = {
			Monday: { '17:30': null, '09:30': 2000 },
			Sunday: { '23:30': 123 },
		};
		const expected: (number | null)[] = Array(336).fill(null);
		expected.fill(2000, 19, 35);
		expected[335] = 123;

		// Act
		const limits = decodeDownloadScheduleDays(days);

		// Assert
		expect(limits).toEqual(expected);
	});

	test('Should encode an overnight selection as independent daily points with an explicit Unlimited end', () => {
		// Arrange
		const limits: (number | null)[] = Array(336).fill(null);
		limits.fill(1000, 47, 49);
		limits[335] = 3000;

		// Act
		const days = encodeDownloadScheduleDays(limits);

		// Assert
		expect(days).toEqual({
			Monday: { '23:30': 1000 },
			Tuesday: { '00:00': 1000, '00:30': null },
			Sunday: { '23:30': 3000 },
		});
	});
});
