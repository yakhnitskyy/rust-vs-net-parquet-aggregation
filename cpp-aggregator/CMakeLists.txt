cmake_minimum_required(VERSION 3.24)

project(cpp-aggregator LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(Arrow CONFIG REQUIRED)
find_package(Parquet CONFIG REQUIRED)

add_executable(cpp-aggregator src/main.cpp
        src/main.cpp)

target_compile_features(cpp-aggregator PRIVATE cxx_std_23)

if(TARGET Arrow::arrow_shared)
    set(ARROW_TARGET Arrow::arrow_shared)
elseif(TARGET Arrow::arrow_static)
    set(ARROW_TARGET Arrow::arrow_static)
elseif(TARGET Arrow::arrow)
    set(ARROW_TARGET Arrow::arrow)
else()
    message(FATAL_ERROR "No supported Arrow target found")
endif()

if(TARGET Parquet::parquet_shared)
    set(PARQUET_TARGET Parquet::parquet_shared)
elseif(TARGET Parquet::parquet_static)
    set(PARQUET_TARGET Parquet::parquet_static)
elseif(TARGET Parquet::parquet)
    set(PARQUET_TARGET Parquet::parquet)
else()
    message(FATAL_ERROR "No supported Parquet target found")
endif()

target_link_libraries(cpp-aggregator PRIVATE ${ARROW_TARGET} ${PARQUET_TARGET})

if(WIN32)
    add_custom_command(
        TARGET cpp-aggregator
        POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different $<TARGET_RUNTIME_DLLS:cpp-aggregator> $<TARGET_FILE_DIR:cpp-aggregator>
        COMMAND_EXPAND_LISTS
    )
endif()

if(MINGW)
    get_filename_component(CXX_COMPILER_DIR "${CMAKE_CXX_COMPILER}" DIRECTORY)
    find_file(MINGW_LIBSTDCXX_DLL libstdc++-6.dll PATHS "${CXX_COMPILER_DIR}" NO_DEFAULT_PATH REQUIRED)
    find_file(MINGW_LIBGCC_DLL libgcc_s_seh-1.dll PATHS "${CXX_COMPILER_DIR}" NO_DEFAULT_PATH REQUIRED)
    find_file(MINGW_LIBWINPTHREAD_DLL libwinpthread-1.dll PATHS "${CXX_COMPILER_DIR}" NO_DEFAULT_PATH REQUIRED)

    add_custom_command(
        TARGET cpp-aggregator
        POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${MINGW_LIBSTDCXX_DLL}"
            "${MINGW_LIBGCC_DLL}"
            "${MINGW_LIBWINPTHREAD_DLL}"
            $<TARGET_FILE_DIR:cpp-aggregator>
    )
endif()
