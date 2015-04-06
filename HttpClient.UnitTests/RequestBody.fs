﻿module HttpClient.Tests.RequestBody

open System
open System.Text

open Fuchu
open HttpClient

let VALID_URL = "http://www"
let createValidRequest = createRequest Get VALID_URL
let utf8 = Encoding.UTF8

[<Tests>]
let apiUsage =
    testList "api usage" [
        testCase "withBody sets the request body" <| fun _ ->
            Assert.Equal((createValidRequest |> withBody """Hello mum!%2\/@$""").Body, BodyString """Hello mum!%2\/@$""")

        testCase "withBody uses default character encoding of UTF-8" <| fun _ ->
            Assert.Equal((createValidRequest |> withBody "whatever").BodyCharacterEncoding, utf8)

        testCase "withBodyEncoded sets the request body" <| fun _ ->
            Assert.Equal((createValidRequest |> withBodyEncoded """Hello mum!%2\/@$""" utf8).Body,
                         BodyString """Hello mum!%2\/@$""")

        testCase "withBodyEncoded sets the body encoding" <| fun _ ->
            Assert.Equal((createValidRequest |> withBodyEncoded "Hi Mum" utf8).BodyCharacterEncoding,
                         utf8)
    ]

[<Tests>]
let bodyFormatting =
    let testSeed = 1234567765

    testList "formatting different sorts of body" [
        testCase "can format raw" <| fun _ ->
            let clientState = { DefaultHttpClientState with random = Random testSeed }
            let body = Impl.formatBody clientState (utf8, BodyRaw [|1uy; 2uy; 3uy|])
            Assert.Equal("body should be verbatim", [|1uy; 2uy; 3uy|], body)

        testCase "can format multipart/formdata single file" <| fun _ ->
            /// can't lift outside, because test cases may run in parallel
            let clientState = { DefaultHttpClientState with random = Random testSeed }

            let fileCt, fileContents =
                ContentType.Parse "text/plain" |> Option.get,
                "Hello World"

            let form =
                // example from http://www.w3.org/TR/html401/interact/forms.html
                [   NameValue { name = "submit-name"; value = "Larry" }
                    SingleFile ("files", ("file1.txt", fileCt, Plain fileContents)) ]

            let subject = Impl.formatBody clientState (utf8, BodyForm form) |> utf8.GetString
            let expected = [ "Content-Type: multipart/form-data; boundary=mACKqCcIID-J''_PL:hfbFiOLC/cew"
                             ""
                             "--mACKqCcIID-J''_PL:hfbFiOLC/cew"
                             "Content-Disposition: form-data; name=\"submit-name\""
                             ""
                             "Larry"
                             "--mACKqCcIID-J''_PL:hfbFiOLC/cew"
                             "Content-Disposition: form-data; name=\"files\"; filename=\"file1.txt\""
                             "Content-Type: text/plain"
                             ""
                             "Hello World"
                             "--mACKqCcIID-J''_PL:hfbFiOLC/cew--" ]
                           |> String.concat "\r\n"
            Assert.Equal("should have correct body", expected, subject)

        testCase "can format multipart/formdata with multipart/mixed for multi-file upload" <| fun _ ->
            /// can't lift outside, because test cases may run in parallel
            let clientState = { DefaultHttpClientState with random = Random testSeed }

            let firstCt, secondCt, fileContents =
                ContentType.Parse "text/plain" |> Option.get,
                ContentType.Parse "text/plain" |> Option.get,
                "Hello World"

            let form =
                // example from http://www.w3.org/TR/html401/interact/forms.html
                [   NameValue { name = "submit-name"; value = "Larry" }
                    MultiFile ("files",
                               [ "file1.txt", firstCt, Plain fileContents
                                 "file2.gif", secondCt, Plain "...contents of file2.gif..."
                               ])
                ]

            let subject = Impl.formatBody clientState (utf8, BodyForm form) |> utf8.GetString
            let expected =
                [ "Content-Type: multipart/form-data; boundary=mACKqCcIID-J''_PL:hfbFiOLC/cew"
                  ""
                  "--mACKqCcIID-J''_PL:hfbFiOLC/cew"
                  "Content-Disposition: form-data; name=\"submit-name\""
                  ""
                  "Larry"
                  "--mACKqCcIID-J''_PL:hfbFiOLC/cew"
                  "Content-Type: multipart/mixed; boundary=iDnsCZhfTqMSYsj:LhBTftNfVog:eA"
                  "Content-Disposition: form-data; name=\"files\""
                  ""
                  "--iDnsCZhfTqMSYsj:LhBTftNfVog:eA"
                  "Content-Disposition: file; filename=\"file1.txt\""
                  "Content-Type: text/plain"
                  ""
                  "Hello World"
                  "--iDnsCZhfTqMSYsj:LhBTftNfVog:eA"
                  "Content-Disposition: file; filename=\"file2.gif\""
                  "Content-Type: text/plain"
                  ""
                  "...contents of file2.gif..."
                  "--iDnsCZhfTqMSYsj:LhBTftNfVog:eA--"
                  "--mACKqCcIID-J''_PL:hfbFiOLC/cew--" ]
                |> String.concat "\r\n"
            Assert.Equal("should have correct body", expected, subject)

        testCase "can format urlencoded data" <| fun _ ->
            // http://www.url-encode-decode.com/
            // https://unspecified.wordpress.com/2008/07/08/browser-uri-encoding-the-best-we-can-do/
            // http://stackoverflow.com/questions/912811/what-is-the-proper-way-to-url-encode-unicode-characters
            // https://www.ietf.org/rfc/rfc1738.txt
            // http://www.w3.org/TR/html401/interact/forms.html
            // http://stackoverflow.com/questions/4007969/application-x-www-form-urlencoded-or-multipart-form-data
            Assert.Equal("Should encode Swedish properly",
                         "user_name=%c3%85sa+den+R%c3%b6de&user_pass=Bovi%c4%87",
                         Impl.uriEncode utf8 [
                            { name = "user_name"; value = "Åsa den Röde" }
                            { name = "user_pass"; value = "Bović" }
                         ])

        testCase "can format urlencoded form" <| fun _ ->
            let clientState = { DefaultHttpClientState with random = Random testSeed }
            // example from http://www.w3.org/TR/html401/interact/forms.html
            [   NameValue { name = "submit"; value = "Join Now!" }
                NameValue { name = "user_name"; value = "Åsa den Röde" }
                NameValue { name = "user_pass"; value = "Bović" }
            ]
            |> fun form -> Impl.formatBody clientState (utf8, BodyForm form)
            |> utf8.GetString
            |> fun result ->
                Assert.Equal(result, "submit=Join+Now!&user_name=%c3%85sa+den+R%c3%b6de&user_pass=Bovi%c4%87")
    ]

let useCase = testCase

(* Use cases for RequestBody.

   It should be possible to send data using all formattings that are common
   to use - raw, urlencoded and formdata.

   A good reference is the implementation of the MailGun API since it really
   uses HTTP as an application protocol.

   https://documentation.mailgun.com/api-sending.html
*)

[<Tests>]
let useCases =
    testList "use cases" [
        testList "sending forms" [
            useCase "sending key-value pairs (urlencoded style)" <| fun _ ->
                Tests.failtest "TODO"

            useCase "sending key-value paris (formdata style)" <| fun _ ->
                Tests.failtest "TODO"
        ]

        testList "sending files" [
            useCase "regular files" <| fun _ ->
                Tests.failtest "TODO"

            useCase "sending UTF8 file names" <| fun _ ->
                Tests.failtest "TODO"
        ]

        testCase "sending raw bytes" <| fun _ ->
            Tests.failtest "TODO"

        testCase "sending using socket" <| fun _ ->
            Tests.failtest "TODO"
    ]