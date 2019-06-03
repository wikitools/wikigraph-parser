import json
import time
import re
import pickle
import sys
import os

DUMPS_PATH = sys.argv[1] if len(sys.argv) >= 2 else '/'
if not DUMPS_PATH.endswith('/'):
	DUMPS_PATH = DUMPS_PATH + '/'
DATA_FILES_FOLDER = 'category-files/'
DUMPS_VERSION = sys.argv[2] if len(sys.argv) >= 3 else 'latest'

TITLE_ID_MAP_FILE_NAME = 'cat_title_to_id.map'
TITLE_ID_MAP_PATH = DATA_FILES_FOLDER + TITLE_ID_MAP_FILE_NAME
OUTFILE = 'cat_links.map'


def create_title_to_id_map():
	title_to_id = {}
	start = time.time()
	with open(DUMPS_PATH + 'enwiki-' + DUMPS_VERSION + '-category.sql', encoding="UTF-8", errors='ignore') as f:
		line_no = 0
		for line in f:
			temp_pageid = 0  # used for print every x lines
			if not line.startswith('INSERT INTO'):  # ignoring create and headers
				continue
			if len(line.split(' ')) != 5:  # line has to have minimum 5 parts when it's an insert
				print('Inserts line ' + str(line_no) + ' has unusual number of spaces ' + str(len(line.split(' '))))
			inserts = line.split(' ')[-1]  # extracting part with values to insert in one string
			value_list = inserts.split('),(')
			#value_list = re.findall(r"(\d+,\d+,'(?:[^'\\]|\\.)*',\d+)", inserts)
			# extracting string with 4 values, separated by comma and backslashes
			for i in range(len(value_list)):
				values = value_list[i].split(',')  # splittig each of 4 values separately
				if i == 0:
					title_to_id[values[1][1:-1]] = int(values[0][1:])
				else:
					title_to_id[values[1][1:-1]] = int(values[0])
			line_no += 1


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
	with open(DUMPS_PATH + 'enwiki-' + DUMPS_VERSION + '-categorylinks.sql', encoding="UTF-8", errors='ignore') as f:
		# opening pagelinks file - encoding errors
		line_no = 0

		for line in f:
			temp_pageid = 0  # used for print every x lines
			if not line.startswith('INSERT INTO'):  # ignoring create and headers
				continue
			if len(line.split(' ')) < 5:  # line has to have minimum 5 parts when it's an insert
				print('Inserts line ' + str(line_no) + ' has unusual number of spaces ' + str(len(line.split(' '))))
			inserts = line.split(' ')[4:]  # extracting part with values to insert in one string
			inserts = ''.join(inserts)
			value_list = inserts.split('),(')
			# extracting string with 4 values, separated by comma and backslashes
			for i in range(len(value_list)):
				values = value_list[i].split(',')  # splittig each of 7 values separately
				title = values[1][1:-1]
				# extracting page id
				if i == 0:
					page_id = int(values[0][1:])  #deleting bracket in first occurance
				else:
					try:
						page_id = int(values[0])
					except:
						page_id = 0
						print('bad id! - Line number'+str(line_no)+', inserted data number: '+str(i))
				temp_pageid = page_id
				if not page_id in links:
					links[page_id] = []  # if occurs for the first time, create empty value
				if title in title_to_id:
					links[page_id].append(
						title_to_id[title])  # if managed to find id based on title, append it as child to page
			if line_no % 100 == 0:
				print(str(line_no)+' - '+str(temp_pageid) + ": " + str(links[temp_pageid]))
			line_no += 1

		#	if 0 <= lines_to_proccess < line_no:
		#		break
	print('Article links map created in: ' + str(time.time() - start) + 's.')

	return links


def createJSON(links):
	json_object = {}
	json_object['catlinks'] = []

	for parent, child in links.items():
		catlinks = str(parent)
		for el in child:
			catlinks += "," + str(el)  # creating long string consisted of ID and children IDs based on links dict
		json_object['catlinks'].append({
			'cl': catlinks
		})
	with open(DUMPS_PATH + OUTFILE, 'w') as outfile:
		json.dump(json_object, outfile)


if not os.path.isfile(TITLE_ID_MAP_PATH):
	if not os.path.exists("category-files/"):
		os.makedirs(os.path.dirname(TITLE_ID_MAP_PATH))
	save_title_to_id_map()
title_to_id = load_title_to_id_map()
createJSON(create_page_links_map(400))
