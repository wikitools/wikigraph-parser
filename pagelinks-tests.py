import time
import re
import pickle
import sys
import os


DUMPS_PATH = sys.argv[1] if len(sys.argv) >= 2 else '/'
if not DUMPS_PATH.endswith('/'):
	DUMPS_PATH = DUMPS_PATH + '/'
DATA_FILES_FOLDER = 'data-files/'
DUMPS_VERSION = sys.argv[2] if len(sys.argv) >= 3 else 'latest'

TITLE_ID_MAP_FILE_NAME = 'title_to_id.map'
TITLE_ID_MAP_PATH = DATA_FILES_FOLDER + TITLE_ID_MAP_FILE_NAME


def create_title_to_id_map():
	title_to_id = {}
	start = time.time()
	with open(DUMPS_PATH + 'enwiki-' + DUMPS_VERSION + '-pages-articles-multistream-index.txt') as f:
		for line in f:
			line = line[:-1]
			parts = line.split(':', maxsplit=2)
			if not parts[2].startswith('Category') and parts[2].__contains__(':'):
				continue
			title_to_id[parts[2]] = int(parts[1])
	print('Article title to id map created in: ' + str(time.time() - start) + 's.')
	return title_to_id


def save_title_to_id_map():
	title_to_id = create_title_to_id_map()
	with open(TITLE_ID_MAP_PATH, 'wb+') as map:
		pickle.dump(title_to_id, map)


def load_title_to_id_map():
	start = time.time()
	with open(TITLE_ID_MAP_PATH, 'rb') as map:
		title_to_id = pickle.load(map)
		print('Article title to id map loaded in: ' + str(time.time() - start) + 's.')
		return title_to_id


def create_page_links_map(lines_to_proccess=-1, inserts_per_line_to_proccess=-1):
	start = time.time()
	links = {}
	with open(DUMPS_PATH + 'enwiki-' + DUMPS_VERSION + '-pagelinks.sql') as f:
		line_no = 0

		for line in f:
			if not line.startswith('INSERT INTO'):
				continue
			if len(line.split(' ')) != 5:
				print('Inserts line ' + str(line_no) + ' has unusual number of spaces ' + str(len(line.split(' '))))
			inserts = line.split(' ')[-1]
			value_list = re.findall(r"(\d+,\d+,'(?:[^'\\]|\\.)*',\d+)", inserts)
			for i in range(len(value_list)):
				if 0 <= inserts_per_line_to_proccess < i:
					break
				values = value_list[i].split(',')
				title = values[2].replace('_', ' ').replace('\\', '')[1:-1]
				page_id = int(values[0])

				if not page_id in links:
					links[page_id] = []
				if title in title_to_id:
					links[page_id].append(title_to_id[title])

			line_no += 1
			if 0 <= lines_to_proccess < line_no:
				break
	print('Article links map created in: ' + str(time.time() - start) + 's.')
	return links


if not os.path.isfile(TITLE_ID_MAP_PATH):
	os.makedirs(os.path.dirname(TITLE_ID_MAP_PATH))
	save_title_to_id_map()
title_to_id = load_title_to_id_map()
links = create_page_links_map(lines_to_proccess=100, inserts_per_line_to_proccess=100)

print(links)
