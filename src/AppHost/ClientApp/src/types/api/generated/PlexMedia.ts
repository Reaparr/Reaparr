/* eslint-disable */
/* tslint:disable */
// @ts-nocheck
/*
 * ---------------------------------------------------------------
 * ## THIS FILE WAS GENERATED VIA SWAGGER-TYPESCRIPT-API        ##
 * ##                                                           ##
 * ## AUTHOR: acacode                                           ##
 * ## SOURCE: https://github.com/acacode/swagger-typescript-api ##
 * ---------------------------------------------------------------
 */

import type { RequestParams } from "./http-client";

import type {
  PlexMediaComparisonDetailsDTO,
  PlexMediaComparisonState,
  PlexMediaDTO,
  PlexMediaStatisticsDTO,
  PlexMediaType,
} from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class PlexMedia {
  /**
   * No description
   * * @tags Plexmedia
   * @name GetAllMediaByTypeEndpoint
   * @request GET:/api/PlexMedia
   * @secure
   */
  getAllMediaByTypeEndpoint = (
    query: {
      comparisonState?: PlexMediaComparisonState;
      /** @format int32 */
      countryId?: number | null;
      /** @default false */
      filterOfflineMedia: boolean;
      /** @default false */
      filterOwnedMedia: boolean;
      /** @format int32 */
      genreId?: number | null;
      mediaType: PlexMediaType;
      /** @format int32 */
      page?: number | null;
      /** @format int32 */
      plexLibraryId?: number | null;
      q?: string | null;
      /** @format int32 */
      qualityId?: number | null;
      /** @format int32 */
      roleId?: number | null;
      /** @format int32 */
      size?: number | null;
      sort?: string | null;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexMediaStatisticsDTO>({
      url: `/api/PlexMedia`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexMediaStatisticsDTO>);

  /**
   * No description
   * * @tags Plexmedia
   * @name GetMediaComparisonDetailsEndpoint
   * @request GET:/api/PlexMedia/comparison-details/{plexMediaId}
   * @secure
   */
  getMediaComparisonDetailsEndpoint = (
    plexMediaId: number,
    query: {
      type: PlexMediaType;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexMediaComparisonDetailsDTO>({
      url: `/api/PlexMedia/comparison-details/${plexMediaId}`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexMediaComparisonDetailsDTO>);

  /**
   * No description
   * * @tags Plexmedia
   * @name GetMediaDetailByIdEndpoint
   * @request GET:/api/PlexMedia/detail/{plexMediaId}
   * @secure
   */
  getMediaDetailByIdEndpoint = (
    plexMediaId: number,
    query: {
      type: PlexMediaType;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<PlexMediaDTO>({
      url: `/api/PlexMedia/detail/${plexMediaId}`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<PlexMediaDTO>);

  /**
   * @description Proxies image bytes from Plex servers with CORS headers.
   * * @tags Plexmedia
   * @name GetPlexMediaThumbnailImageEndpoint
   * @summary Proxy Plex image
   * @request GET:/api/PlexMedia/thumbnail
   * @secure
   */
  getPlexMediaThumbnailImageEndpoint = (
    query: {
      /**
       * @format int32
       * @example 400
       */
      height: number;
      /**
       * @format int32
       * @example 57920
       */
      metaDataKey: number;
      /**
       * @format int32
       * @example 1756014789
       */
      plexKey: number;
      /**
       * @format int32
       * @example 1
       */
      plexServerId: number;
      /**
       * @format int32
       * @example 200
       */
      width: number;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<Blob>({
      url: `/api/PlexMedia/thumbnail`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "blob",
      ...params,
    }).pipe(apiCheckPipe<Blob>);
}

export class PlexMediaPaths {
  static getAllMediaByTypeEndpoint = (query: {
    comparisonState?: PlexMediaComparisonState;
    /** @format int32 */
    countryId?: number | null;
    /** @default false */
    filterOfflineMedia: boolean;
    /** @default false */
    filterOwnedMedia: boolean;
    /** @format int32 */
    genreId?: number | null;
    mediaType: PlexMediaType;
    /** @format int32 */
    page?: number | null;
    /** @format int32 */
    plexLibraryId?: number | null;
    q?: string | null;
    /** @format int32 */
    qualityId?: number | null;
    /** @format int32 */
    roleId?: number | null;
    /** @format int32 */
    size?: number | null;
    sort?: string | null;
  }) => queryString.stringifyUrl({ url: `/api/PlexMedia`, query });

  static getMediaComparisonDetailsEndpoint = (
    plexMediaId: number,
    query: {
      type: PlexMediaType;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexMedia/comparison-details/${plexMediaId}`,
      query,
    });

  static getMediaDetailByIdEndpoint = (
    plexMediaId: number,
    query: {
      type: PlexMediaType;
    },
  ) =>
    queryString.stringifyUrl({
      url: `/api/PlexMedia/detail/${plexMediaId}`,
      query,
    });

  static getPlexMediaThumbnailImageEndpoint = (query: {
    /**
     * @format int32
     * @example 400
     */
    height: number;
    /**
     * @format int32
     * @example 57920
     */
    metaDataKey: number;
    /**
     * @format int32
     * @example 1756014789
     */
    plexKey: number;
    /**
     * @format int32
     * @example 1
     */
    plexServerId: number;
    /**
     * @format int32
     * @example 200
     */
    width: number;
  }) => queryString.stringifyUrl({ url: `/api/PlexMedia/thumbnail`, query });
}
