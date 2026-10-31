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

import type { JobStatusUpdateDTO } from "./data-contracts";

import { apiCheckPipe, axiosObservable } from "@api/base";
import queryString from "query-string";

export class BackgroundJobs {
  /**
   * No description
   * * @tags Backgroundjobs
   * @name GetAllBackgroundJobsEndpoint
   * @request GET:/api/BackgroundJobs
   * @secure
   */
  getAllBackgroundJobsEndpoint = (
    query?: {
      UseMockData?: boolean;
    },
    params: RequestParams = {},
  ) =>
    axiosObservable<JobStatusUpdateDTO[]>({
      url: `/api/BackgroundJobs`,
      method: "GET",
      params: query,
      secure: true,
      responseType: "json",
      ...params,
    }).pipe(apiCheckPipe<JobStatusUpdateDTO[]>);
}

export class BackgroundJobsPaths {
  static getAllBackgroundJobsEndpoint = (query?: { UseMockData?: boolean }) =>
    queryString.stringifyUrl({ url: `/api/BackgroundJobs`, query });
}
